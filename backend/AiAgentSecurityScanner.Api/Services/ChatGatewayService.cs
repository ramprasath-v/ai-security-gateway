using System.Diagnostics;
using System.Text.RegularExpressions;
using AiAgentSecurityScanner.Api.Data;
using AiAgentSecurityScanner.Api.DTOs;
using AiAgentSecurityScanner.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiAgentSecurityScanner.Api.Services;

public interface IChatGatewayService
{
    Task<ChatResponse> SendAsync(ChatRequest request, string? sourceIp, CancellationToken cancellationToken);
}

public class ApplicationValidationException(string message) : Exception(message);

public partial class ChatGatewayService(
    AppDbContext db,
    IAiSecurityDetectionService detectionService,
    IPolicyService policyService,
    ILLMProviderResolver providerResolver,
    ISemanticCacheService semanticCacheService,
    IOptions<AiSecurityPolicyOptions> policyOptions,
    ILogger<ChatGatewayService> logger) : IChatGatewayService
{
    public async Task<ChatResponse> SendAsync(ChatRequest request, string? sourceIp, CancellationToken cancellationToken)
    {
        var gatewayStopwatch = Stopwatch.StartNew();
        var requestStartedAtUtc = DateTime.UtcNow;
        var prompt = request.Message?.Trim() ?? string.Empty;
        var promptLength = prompt.Length;
        var llmProvider = providerResolver.Resolve(request.Provider);
        var provider = llmProvider.ProviderName;
        var requestedModel = string.IsNullOrWhiteSpace(request.Model) ? llmProvider.ModelName : request.Model.Trim();
        var application = await ResolveApplicationAsync(request.ApplicationId, request.AppKey, cancellationToken);

        var session = await GetOrCreateSessionAsync(request.SessionId, prompt, cancellationToken);
        db.ChatMessages.Add(new ChatMessage
        {
            Session = session,
            Role = "user",
            Content = prompt,
            CreatedAtUtc = requestStartedAtUtc,
            Model = requestedModel
        });

        var policyStopwatch = Stopwatch.StartNew();
        var detection = detectionService.Analyze(prompt);
        var decision = await policyService.DecideAsync(prompt, detection, cancellationToken);
        policyStopwatch.Stop();

        var model = requestedModel;
        var status = "Completed";
        var responseText = string.Empty;
        string? errorMessage = null;
        long? ollamaDurationMs = null;
        int? promptTokens = null;
        int? responseTokens = null;
        int? totalTokens = null;
        var llmTokensUsed = 0;
        var tokensSaved = 0;
        var cachedTokensServed = 0;
        var wasBlockedBeforeModelCall = decision.Action == "block";
        SemanticCacheLookupResult? cacheLookup = null;
        var cacheKey = (string?)null;
        var cacheHit = false;
        var exactCacheHit = false;
        var semanticCacheHit = false;
        double? similarityScore = null;

        logger.LogInformation(
            "Gateway request starting. Application={ApplicationName} ApplicationEnvironment={ApplicationEnvironment} Provider={Provider} Model={Model} PromptLength={PromptLength} PolicyAction={PolicyAction} PolicyCheckDurationMs={PolicyCheckDurationMs} RiskScore={RiskScore}",
            application.Name,
            application.Environment,
            provider,
            model,
            promptLength,
            decision.Action,
            policyStopwatch.ElapsedMilliseconds,
            decision.RiskScore);

        if (wasBlockedBeforeModelCall)
        {
            status = "Blocked";
            responseText = "Request blocked by AI security policy. Remove secrets or credentials and try again.";
        }
        else
        {
            cacheLookup = await semanticCacheService.LookupAsync(prompt, provider, model, decision.Action, llmProvider, cancellationToken);
            cacheKey = cacheLookup.CacheKey;
            cacheHit = cacheLookup.CacheHit;
            exactCacheHit = cacheLookup.ExactCacheHit;
            semanticCacheHit = cacheLookup.SemanticCacheHit;
            similarityScore = cacheLookup.SimilarityScore;

            if (cacheLookup.CacheHit && cacheLookup.CachedResponse is not null)
            {
                responseText = cacheLookup.CachedResponse;
                logger.LogInformation(
                    "Gateway response served from cache. CacheKey={CacheKey} ExactCacheHit={ExactCacheHit} SemanticCacheHit={SemanticCacheHit} CachedResponseLength={CachedResponseLength}",
                    cacheKey,
                    exactCacheHit,
                    semanticCacheHit,
                    responseText.Length);
                promptTokens = cacheLookup.PromptTokens;
                responseTokens = cacheLookup.ResponseTokens;
                cachedTokensServed = TokenTotal(cacheLookup.PromptTokens, cacheLookup.ResponseTokens, cacheLookup.TotalTokens) ?? 0;
                tokensSaved = cachedTokensServed;
                totalTokens = 0;
            }
            else
            {
                try
                {
                    var llm = await llmProvider.SendChatAsync(decision.PromptToSend, model, cancellationToken);
                    provider = llm.Provider;
                    model = llm.Model;
                    responseText = llm.Content;
                    ollamaDurationMs = llm.DurationMs;
                    promptTokens = llm.PromptTokens;
                    responseTokens = llm.ResponseTokens;
                    totalTokens = TokenTotal(llm.PromptTokens, llm.ResponseTokens, llm.TotalTokens);
                    llmTokensUsed = totalTokens ?? 0;
                    if (status == "Completed" && IsCompleteLlmResponse(llm.DoneReason))
                    {
                        logger.LogInformation(
                            "Gateway response ready for cache. Provider={Provider} Model={Model} ResponseLengthBeforeCaching={ResponseLengthBeforeCaching} StreamedChunksReceived={StreamedChunksReceived} DoneReason={DoneReason}",
                            provider,
                            model,
                            responseText.Length,
                            llm.StreamedChunksReceived,
                            llm.DoneReason);
                        await semanticCacheService.SaveAsync(
                            prompt,
                            cacheLookup.NormalizedPrompt,
                            cacheLookup.Embedding,
                            responseText,
                            provider,
                            model,
                            decision.Action,
                            promptTokens,
                            responseTokens,
                            totalTokens,
                            cancellationToken);
                    }
                    else if (status == "Completed")
                    {
                        logger.LogWarning(
                            "Skipping semantic cache save for incomplete LLM response. Provider={Provider} Model={Model} ResponseLengthBeforeCaching={ResponseLengthBeforeCaching} StreamedChunksReceived={StreamedChunksReceived} DoneReason={DoneReason}",
                            provider,
                            model,
                            responseText.Length,
                            llm.StreamedChunksReceived,
                            llm.DoneReason);
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                {
                    status = "Failed";
                    errorMessage = ex is TaskCanceledException ? $"{provider} request timed out." : ex.Message;
                    responseText = $"{provider} is not reachable or timed out. Check the configured provider and model.";
                    logger.LogWarning(ex, "Gateway request failed while calling LLM provider. Provider={Provider} Model={Model} PromptLength={PromptLength}", provider, model, promptLength);
                }
            }
        }

        var policy = policyOptions.Value;
        var log = new ChatRequestLog
        {
            RequestStartedAtUtc = requestStartedAtUtc,
            PolicyCheckDurationMs = policyStopwatch.ElapsedMilliseconds,
            OllamaDurationMs = ollamaDurationMs,
            PromptTokens = promptTokens,
            ResponseTokens = responseTokens,
            TotalTokens = totalTokens,
            LlmTokensUsed = llmTokensUsed,
            TokensSaved = tokensSaved,
            CachedTokensServed = cachedTokensServed,
            CacheHit = cacheHit,
            ExactCacheHit = exactCacheHit,
            SemanticCacheHit = semanticCacheHit,
            SimilarityScore = similarityScore,
            CacheKey = cacheKey,
            MatchedCachePrompt = cacheLookup?.MatchedPrompt,
            WasBlockedBeforeModelCall = wasBlockedBeforeModelCall,
            ApplicationId = application.Id,
            ApplicationKey = application.AppKey,
            ApplicationName = application.Name,
            ApplicationOwner = application.Owner,
            ApplicationEnvironment = application.Environment,
            Provider = provider,
            Model = model,
            PromptText = prompt,
            ResponseText = responseText,
            PromptPreview = Truncate(detection.RedactedText, policy.PromptPreviewMaxLength),
            ResponsePreview = Truncate(responseText, policy.ResponsePreviewMaxLength),
            PromptLength = promptLength,
            ResponseLength = responseText.Length,
            Status = status,
            PolicyAction = decision.Action,
            PolicyReason = decision.Reason,
            ErrorMessage = errorMessage,
            RiskScore = decision.RiskScore,
            MatchedPolicyName = decision.MatchedPolicies.FirstOrDefault()?.Name,
            SourceIp = sourceIp
        };

        foreach (var finding in detection.Findings)
        {
            log.Findings.Add(new SensitiveDataFinding
            {
                Type = finding.Type,
                Severity = finding.Severity,
                Description = finding.Description
            });
        }

        db.ChatRequestLogs.Add(log);
        db.ChatMessages.Add(new ChatMessage
        {
            Session = session,
            Role = "assistant",
            Content = responseText,
            CreatedAtUtc = DateTime.UtcNow,
            Model = model,
            PolicyDecision = decision.Action,
            RiskScore = decision.RiskScore,
            RequestId = log.Id
        });
        session.UpdatedAtUtc = DateTime.UtcNow;

        var dbStopwatch = Stopwatch.StartNew();
        await db.SaveChangesAsync(cancellationToken);
        dbStopwatch.Stop();
        gatewayStopwatch.Stop();

        log.RequestCompletedAtUtc = DateTime.UtcNow;
        log.DatabaseDurationMs = dbStopwatch.ElapsedMilliseconds;
        log.TotalDurationMs = gatewayStopwatch.ElapsedMilliseconds;
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Gateway request completed. RequestId={RequestId} Application={ApplicationName} ApplicationEnvironment={ApplicationEnvironment} Provider={Provider} Model={Model} Status={Status} PolicyAction={PolicyAction} PromptLength={PromptLength} ResponseLength={ResponseLength} ResponseLengthReturnedByGateway={ResponseLengthReturnedByGateway} PromptTokens={PromptTokens} ResponseTokens={ResponseTokens} TotalTokens={TotalTokens} LlmTokensUsed={LlmTokensUsed} TokensSaved={TokensSaved} CachedTokensServed={CachedTokensServed} CacheHit={CacheHit} ExactCacheHit={ExactCacheHit} SemanticCacheHit={SemanticCacheHit} SimilarityScore={SimilarityScore} PolicyCheckDurationMs={PolicyCheckDurationMs} LlmDurationMs={LlmDurationMs} DatabaseDurationMs={DatabaseDurationMs} TotalDurationMs={TotalDurationMs}",
            log.Id,
            application.Name,
            application.Environment,
            provider,
            model,
            status,
            decision.Action,
            promptLength,
            responseText.Length,
            responseText.Length,
            promptTokens,
            responseTokens,
            totalTokens,
            llmTokensUsed,
            tokensSaved,
            cachedTokensServed,
            cacheHit,
            exactCacheHit,
            semanticCacheHit,
            similarityScore,
            policyStopwatch.ElapsedMilliseconds,
            ollamaDurationMs,
            dbStopwatch.ElapsedMilliseconds,
            gatewayStopwatch.ElapsedMilliseconds);

        return new ChatResponse(
            log.Id,
            session.Id,
            application.Id,
            application.AppKey,
            application.Name,
            application.Owner,
            application.Environment,
            provider,
            model,
            status,
            decision.Action,
            decision.Reason,
            responseText,
            decision.RiskScore,
            log.TotalDurationMs,
            policyStopwatch.ElapsedMilliseconds,
            ollamaDurationMs,
            dbStopwatch.ElapsedMilliseconds,
            promptLength,
            responseText.Length,
            promptTokens,
            responseTokens,
            totalTokens,
            llmTokensUsed,
            tokensSaved,
            cachedTokensServed,
            cacheHit,
            exactCacheHit,
            semanticCacheHit,
            similarityScore,
            wasBlockedBeforeModelCall,
            errorMessage,
            detection.Findings.Select(x => new SensitiveFindingDto(x.Type, x.Severity, x.Description)).ToList());
    }

    private async Task<AiApplication> ResolveApplicationAsync(Guid? applicationId, string? appKey, CancellationToken cancellationToken)
    {
        AiApplication? application = null;
        if (applicationId is not null)
        {
            application = await db.AiApplications.FirstOrDefaultAsync(x => x.Id == applicationId, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(appKey))
        {
            var key = appKey.Trim();
            application = await db.AiApplications.FirstOrDefaultAsync(x => x.AppKey == key, cancellationToken);
        }
        else
        {
            application = await db.AiApplications.FirstOrDefaultAsync(x => x.AppKey == "ai-chat-portal", cancellationToken);
        }

        if (application is null)
            throw new ApplicationValidationException("Application does not exist.");

        if (!application.Enabled)
            throw new ApplicationValidationException($"Application '{application.Name}' is disabled.");

        return application;
    }

    private async Task<ChatSession> GetOrCreateSessionAsync(Guid? sessionId, string prompt, CancellationToken cancellationToken)
    {
        if (sessionId is not null)
        {
            var existing = await db.ChatSessions.FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
            if (existing is not null) return existing;
        }

        var session = new ChatSession { Title = BuildTitle(prompt) };
        db.ChatSessions.Add(session);
        return session;
    }

    private static string BuildTitle(string prompt)
    {
        var title = WhitespacePattern().Replace(prompt.Trim(), " ");
        if (string.IsNullOrWhiteSpace(title)) return "New chat";
        return title.Length <= 42 ? title : title[..42] + "…";
    }

    private static string Truncate(string value, int maxLength)
    {
        if (maxLength <= 0) return string.Empty;
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static int? TokenTotal(int? promptTokens, int? responseTokens, int? reportedTotal)
    {
        if (promptTokens is not null || responseTokens is not null)
            return (promptTokens ?? 0) + (responseTokens ?? 0);

        return reportedTotal;
    }

    private static bool IsCompleteLlmResponse(string? doneReason)
    {
        return string.IsNullOrWhiteSpace(doneReason) || string.Equals(doneReason, "stop", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}
