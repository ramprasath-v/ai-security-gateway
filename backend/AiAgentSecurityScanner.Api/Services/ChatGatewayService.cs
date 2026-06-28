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

public partial class ChatGatewayService(
    AppDbContext db,
    IAiSecurityDetectionService detectionService,
    IPolicyService policyService,
    IOllamaService ollamaService,
    ISemanticCacheService semanticCacheService,
    IOptions<OllamaOptions> ollamaOptions,
    IOptions<AiSecurityPolicyOptions> policyOptions,
    ILogger<ChatGatewayService> logger) : IChatGatewayService
{
    public async Task<ChatResponse> SendAsync(ChatRequest request, string? sourceIp, CancellationToken cancellationToken)
    {
        var gatewayStopwatch = Stopwatch.StartNew();
        var requestStartedAtUtc = DateTime.UtcNow;
        var prompt = request.Message?.Trim() ?? string.Empty;
        var promptLength = prompt.Length;
        var requestedModel = string.IsNullOrWhiteSpace(request.Model) ? ollamaOptions.Value.DefaultModel : request.Model.Trim();

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
        var decision = policyService.Decide(prompt, detection);
        policyStopwatch.Stop();

        var model = requestedModel;
        var status = "Completed";
        var responseText = string.Empty;
        string? errorMessage = null;
        long? ollamaDurationMs = null;
        int? promptTokens = null;
        int? responseTokens = null;
        int? totalTokens = null;
        var wasBlockedBeforeModelCall = decision.Action == "block";
        SemanticCacheLookupResult? cacheLookup = null;
        var cacheKey = (string?)null;
        var cacheHit = false;
        var exactCacheHit = false;
        var semanticCacheHit = false;
        double? similarityScore = null;

        logger.LogInformation(
            "Gateway request starting. Model={Model} PromptLength={PromptLength} PolicyAction={PolicyAction} PolicyCheckDurationMs={PolicyCheckDurationMs} RiskScore={RiskScore}",
            model,
            promptLength,
            decision.Action,
            policyStopwatch.ElapsedMilliseconds,
            detection.RiskScore);

        if (wasBlockedBeforeModelCall)
        {
            status = "Blocked";
            responseText = "Request blocked by AI security policy. Remove secrets or credentials and try again.";
        }
        else
        {
            cacheLookup = await semanticCacheService.LookupAsync(prompt, model, decision.Action, cancellationToken);
            cacheKey = cacheLookup.CacheKey;
            cacheHit = cacheLookup.CacheHit;
            exactCacheHit = cacheLookup.ExactCacheHit;
            semanticCacheHit = cacheLookup.SemanticCacheHit;
            similarityScore = cacheLookup.SimilarityScore;

            if (cacheLookup.CacheHit && cacheLookup.CachedResponse is not null)
            {
                responseText = cacheLookup.CachedResponse;
                promptTokens = cacheLookup.PromptTokens;
                responseTokens = cacheLookup.ResponseTokens;
                totalTokens = cacheLookup.TotalTokens;
            }
            else
            {
                try
                {
                    var ollama = await ollamaService.ChatAsync(decision.PromptToSend, model, cancellationToken);
                    model = ollama.Model;
                    responseText = ollama.Content;
                    ollamaDurationMs = ollama.DurationMs;
                    promptTokens = ollama.PromptTokens;
                    responseTokens = ollama.ResponseTokens;
                    totalTokens = ollama.TotalTokens;
                    if (status == "Completed")
                    {
                        await semanticCacheService.SaveAsync(
                            prompt,
                            cacheLookup.NormalizedPrompt,
                            cacheLookup.Embedding,
                            responseText,
                            model,
                            decision.Action,
                            promptTokens,
                            responseTokens,
                            totalTokens,
                            cancellationToken);
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                {
                    status = "Failed";
                    errorMessage = ex is TaskCanceledException ? "Ollama request timed out." : ex.Message;
                    responseText = "Ollama is not reachable or timed out. Make sure Ollama is running and the local model is pulled.";
                    logger.LogWarning(ex, "Gateway request failed while calling Ollama. Model={Model} PromptLength={PromptLength}", model, promptLength);
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
            CacheHit = cacheHit,
            ExactCacheHit = exactCacheHit,
            SemanticCacheHit = semanticCacheHit,
            SimilarityScore = similarityScore,
            CacheKey = cacheKey,
            WasBlockedBeforeModelCall = wasBlockedBeforeModelCall,
            Model = model,
            PromptPreview = Truncate(detection.RedactedText, policy.PromptPreviewMaxLength),
            ResponsePreview = Truncate(responseText, policy.ResponsePreviewMaxLength),
            PromptLength = promptLength,
            ResponseLength = responseText.Length,
            Status = status,
            PolicyAction = decision.Action,
            PolicyReason = decision.Reason,
            ErrorMessage = errorMessage,
            RiskScore = detection.RiskScore,
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
            RiskScore = detection.RiskScore,
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
            "Gateway request completed. RequestId={RequestId} Model={Model} Status={Status} PolicyAction={PolicyAction} PromptLength={PromptLength} ResponseLength={ResponseLength} PromptTokens={PromptTokens} ResponseTokens={ResponseTokens} TotalTokens={TotalTokens} CacheHit={CacheHit} ExactCacheHit={ExactCacheHit} SemanticCacheHit={SemanticCacheHit} SimilarityScore={SimilarityScore} PolicyCheckDurationMs={PolicyCheckDurationMs} OllamaDurationMs={OllamaDurationMs} DatabaseDurationMs={DatabaseDurationMs} TotalDurationMs={TotalDurationMs}",
            log.Id,
            model,
            status,
            decision.Action,
            promptLength,
            responseText.Length,
            promptTokens,
            responseTokens,
            totalTokens,
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
            model,
            status,
            decision.Action,
            decision.Reason,
            responseText,
            detection.RiskScore,
            log.TotalDurationMs,
            policyStopwatch.ElapsedMilliseconds,
            ollamaDurationMs,
            dbStopwatch.ElapsedMilliseconds,
            promptLength,
            responseText.Length,
            promptTokens,
            responseTokens,
            totalTokens,
            cacheHit,
            exactCacheHit,
            semanticCacheHit,
            similarityScore,
            wasBlockedBeforeModelCall,
            errorMessage,
            detection.Findings.Select(x => new SensitiveFindingDto(x.Type, x.Severity, x.Description)).ToList());
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

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}
