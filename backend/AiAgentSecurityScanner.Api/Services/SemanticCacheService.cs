using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AiAgentSecurityScanner.Api.Data;
using AiAgentSecurityScanner.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiAgentSecurityScanner.Api.Services;

public record SemanticCacheLookupResult(
    bool CacheHit,
    bool ExactCacheHit,
    bool SemanticCacheHit,
    double? SimilarityScore,
    string? CachedResponse,
    int? PromptTokens,
    int? ResponseTokens,
    int? TotalTokens,
    string? CacheKey,
    string? MatchedPrompt,
    string NormalizedPrompt,
    float[]? Embedding);

public interface ISemanticCacheService
{
    Task<SemanticCacheLookupResult> LookupAsync(string prompt, string provider, string model, string policyDecision, string applicationKey, ILLMProvider llmProvider, CancellationToken cancellationToken);
    Task SaveAsync(string originalPrompt, string normalizedPrompt, float[]? embedding, string response, string provider, string model, string policyDecision, string applicationKey, int? promptTokens, int? responseTokens, int? totalTokens, CancellationToken cancellationToken);
}

public partial class SemanticCacheService(
    AppDbContext db,
    IPromptNormalizer normalizer,
    IOptions<SemanticCacheOptions> options,
    ILogger<SemanticCacheService> logger) : ISemanticCacheService
{
    private const int LegacyTruncatedResponseTokenLimit = 120;

    public async Task<SemanticCacheLookupResult> LookupAsync(string prompt, string provider, string model, string policyDecision, string applicationKey, ILLMProvider llmProvider, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var cacheText = BuildCacheComparisonText(prompt);
        var normalized = normalizer.Normalize(cacheText.Text);
        var promptHash = Sha256Hex(prompt);
        var cacheTextHash = Sha256Hex(cacheText.Text);
        var cacheModelScope = BuildCacheModelScope(provider, model, applicationKey, cacheText.ContextHash, cacheText.Kind);
        var cacheKey = $"{cacheModelScope}|{policyDecision}|{normalized}";
        if (!options.Value.Enabled)
        {
            stopwatch.Stop();
            logger.LogInformation(
                "Semantic cache lookup disabled. Provider={Provider} Model={Model} ApplicationKey={ApplicationKey} PolicyDecision={PolicyDecision} MessageHash={MessageHash} CacheTextKind={CacheTextKind} CacheTextHash={CacheTextHash} CacheTextPreview={CacheTextPreview} DurationMs={DurationMs}",
                provider,
                model,
                applicationKey,
                policyDecision,
                promptHash,
                cacheText.Kind,
                cacheTextHash,
                Preview(cacheText.Text),
                stopwatch.ElapsedMilliseconds);
            return new(false, false, false, null, null, null, null, null, cacheKey, null, normalized, null);
        }

        var exactMatches = await db.SemanticCacheEntries
            .AsNoTracking()
            .Where(x => x.Model == cacheModelScope && x.PolicyDecision == policyDecision && x.NormalizedPrompt == normalized)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(10)
            .ToListAsync(cancellationToken);

        var exact = exactMatches.FirstOrDefault(x => !LooksLikeLegacyTruncatedResponse(x));
        if (exact is not null)
        {
            stopwatch.Stop();
            logger.LogInformation(
                "Semantic cache lookup complete. Result=ExactHit Provider={Provider} Model={Model} ApplicationKey={ApplicationKey} PolicyDecision={PolicyDecision} MessageHash={MessageHash} CacheTextKind={CacheTextKind} CacheTextHash={CacheTextHash} CacheTextPreview={CacheTextPreview} MatchedPromptHash={MatchedPromptHash} MatchedPromptPreview={MatchedPromptPreview} Similarity={Similarity} DurationMs={DurationMs} CachedResponseLength={CachedResponseLength}",
                provider,
                model,
                applicationKey,
                policyDecision,
                promptHash,
                cacheText.Kind,
                cacheTextHash,
                Preview(cacheText.Text),
                Sha256Hex(exact.OriginalPrompt),
                Preview(exact.OriginalPrompt),
                1.0,
                stopwatch.ElapsedMilliseconds,
                exact.CachedResponse.Length);
            return new(true, true, false, 1.0, exact.CachedResponse, exact.PromptTokens, exact.ResponseTokens, exact.TotalTokens, cacheKey, exact.OriginalPrompt, normalized, null);
        }

        if (exactMatches.Count > 0)
        {
            logger.LogWarning(
                "Bypassing exact cache entries that look truncated. Provider={Provider} Model={Model} PolicyDecision={PolicyDecision} BypassedCount={BypassedCount}",
                provider,
                model,
                policyDecision,
                exactMatches.Count);
        }

        var embedding = await llmProvider.GetEmbeddingAsync(normalized, cancellationToken);
        if (embedding is null || embedding.Length == 0)
        {
            stopwatch.Stop();
            logger.LogInformation(
                "Semantic cache lookup complete. Result=MissNoEmbedding Provider={Provider} Model={Model} ApplicationKey={ApplicationKey} PolicyDecision={PolicyDecision} MessageHash={MessageHash} CacheTextKind={CacheTextKind} CacheTextHash={CacheTextHash} CacheTextPreview={CacheTextPreview} DurationMs={DurationMs}",
                provider,
                model,
                applicationKey,
                policyDecision,
                promptHash,
                cacheText.Kind,
                cacheTextHash,
                Preview(cacheText.Text),
                stopwatch.ElapsedMilliseconds);
            return new(false, false, false, null, null, null, null, null, cacheKey, null, normalized, null);
        }

        var candidates = await db.SemanticCacheEntries
            .AsNoTracking()
            .Where(x => x.Model == cacheModelScope && x.PolicyDecision == policyDecision)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(250)
            .ToListAsync(cancellationToken);

        SemanticCacheEntry? best = null;
        double bestScore = 0;
        var skippedTruncatedCandidates = 0;
        foreach (var candidate in candidates)
        {
            if (LooksLikeLegacyTruncatedResponse(candidate))
            {
                skippedTruncatedCandidates++;
                continue;
            }

            float[]? candidateEmbedding;
            try { candidateEmbedding = JsonSerializer.Deserialize<float[]>(candidate.EmbeddingJson); }
            catch { continue; }
            if (candidateEmbedding is null || candidateEmbedding.Length != embedding.Length) continue;

            var score = CosineSimilarity(embedding, candidateEmbedding);
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        if (best is not null && bestScore >= options.Value.SimilarityThreshold)
        {
            stopwatch.Stop();
            logger.LogInformation(
                "Semantic cache lookup complete. Result=SemanticHit Provider={Provider} Model={Model} ApplicationKey={ApplicationKey} PolicyDecision={PolicyDecision} MessageHash={MessageHash} CacheTextKind={CacheTextKind} CacheTextHash={CacheTextHash} CacheTextPreview={CacheTextPreview} MatchedPromptHash={MatchedPromptHash} MatchedPromptPreview={MatchedPromptPreview} DurationMs={DurationMs} Similarity={Similarity} Threshold={Threshold} CandidateCount={CandidateCount} SkippedTruncatedCandidates={SkippedTruncatedCandidates} CachedResponseLength={CachedResponseLength}",
                provider,
                model,
                applicationKey,
                policyDecision,
                promptHash,
                cacheText.Kind,
                cacheTextHash,
                Preview(cacheText.Text),
                Sha256Hex(best.OriginalPrompt),
                Preview(best.OriginalPrompt),
                stopwatch.ElapsedMilliseconds,
                bestScore,
                options.Value.SimilarityThreshold,
                candidates.Count,
                skippedTruncatedCandidates,
                best.CachedResponse.Length);
            return new(true, false, true, bestScore, best.CachedResponse, best.PromptTokens, best.ResponseTokens, best.TotalTokens, cacheKey, best.OriginalPrompt, normalized, embedding);
        }

        stopwatch.Stop();
        logger.LogInformation(
            "Semantic cache lookup complete. Result=Miss Provider={Provider} Model={Model} ApplicationKey={ApplicationKey} PolicyDecision={PolicyDecision} MessageHash={MessageHash} CacheTextKind={CacheTextKind} CacheTextHash={CacheTextHash} CacheTextPreview={CacheTextPreview} MatchedPromptHash={MatchedPromptHash} MatchedPromptPreview={MatchedPromptPreview} DurationMs={DurationMs} BestSimilarity={Similarity} Threshold={Threshold} CandidateCount={CandidateCount} SkippedTruncatedCandidates={SkippedTruncatedCandidates}",
            provider,
            model,
            applicationKey,
            policyDecision,
            promptHash,
            cacheText.Kind,
            cacheTextHash,
            Preview(cacheText.Text),
            best is null ? null : Sha256Hex(best.OriginalPrompt),
            best is null ? null : Preview(best.OriginalPrompt),
            stopwatch.ElapsedMilliseconds,
            best is null ? null : bestScore,
            options.Value.SimilarityThreshold,
            candidates.Count,
            skippedTruncatedCandidates);
        return new(false, false, false, best is null ? null : bestScore, null, null, null, null, cacheKey, best?.OriginalPrompt, normalized, embedding);
    }

    public Task SaveAsync(string originalPrompt, string normalizedPrompt, float[]? embedding, string response, string provider, string model, string policyDecision, string applicationKey, int? promptTokens, int? responseTokens, int? totalTokens, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled) return Task.CompletedTask;
        if (embedding is null || embedding.Length == 0) return Task.CompletedTask;

        var cacheText = BuildCacheComparisonText(originalPrompt);
        logger.LogInformation(
            "Saving complete response to semantic cache. Provider={Provider} Model={Model} ApplicationKey={ApplicationKey} PolicyDecision={PolicyDecision} MessageHash={MessageHash} ContextHash={ContextHash} CacheTextKind={CacheTextKind} NormalizedCacheTextHash={NormalizedCacheTextHash} ResponseLengthBeforeCaching={ResponseLengthBeforeCaching}",
            provider,
            model,
            applicationKey,
            policyDecision,
            Sha256Hex(originalPrompt),
            cacheText.ContextHash,
            cacheText.Kind,
            Sha256Hex(normalizedPrompt),
            response.Length);

        db.SemanticCacheEntries.Add(new SemanticCacheEntry
        {
            OriginalPrompt = originalPrompt,
            NormalizedPrompt = normalizedPrompt,
            EmbeddingJson = JsonSerializer.Serialize(embedding),
            CachedResponse = response,
            Model = BuildCacheModelScope(provider, model, applicationKey, cacheText.ContextHash, cacheText.Kind),
            PolicyDecision = policyDecision,
            PromptTokens = promptTokens,
            ResponseTokens = responseTokens,
            TotalTokens = totalTokens
        });

        return Task.CompletedTask;
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        double dot = 0;
        double normA = 0;
        double normB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        return normA == 0 || normB == 0 ? 0 : dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }

    private static bool LooksLikeLegacyTruncatedResponse(SemanticCacheEntry entry)
    {
        if (entry.ResponseTokens is null or < LegacyTruncatedResponseTokenLimit) return false;

        var response = entry.CachedResponse.TrimEnd();
        if (response.Length == 0) return true;

        var last = response[^1];
        return last is not ('.' or '!' or '?' or '"' or '\'' or ')' or ']' or '}');
    }

    private static string BuildCacheModelScope(string provider, string model, string applicationKey, string contextHash, string cacheTextKind)
    {
        return $"v3|app:{applicationKey.Trim().ToLowerInvariant()}|provider:{provider.Trim().ToLowerInvariant()}|model:{model.Trim().ToLowerInvariant()}|kind:{cacheTextKind}|ctx:{contextHash}";
    }

    private static CacheComparisonText BuildCacheComparisonText(string prompt)
    {
        var questionMatch = RagQuestionPattern().Match(prompt);
        if (!questionMatch.Success) return new(prompt, "message", "no-context");

        var question = questionMatch.Groups["question"].Value.Trim();
        var contextMatch = RagContextPattern().Match(prompt);
        var context = contextMatch.Success ? contextMatch.Groups["context"].Value.Trim() : string.Empty;
        var contextHash = string.IsNullOrWhiteSpace(context) ? "no-context" : Sha256Hex(context);
        return new(string.IsNullOrWhiteSpace(question) ? prompt : question, "rag-question", contextHash);
    }

    private static string Sha256Hex(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string Preview(string value)
    {
        value = WhitespacePattern().Replace(value.Trim(), " ");
        return value.Length <= 300 ? value : value[..300];
    }

    private sealed record CacheComparisonText(string Text, string Kind, string ContextHash);

    [GeneratedRegex(@"(?is)\bQuestion:\s*(?<question>.+?)\s*$")]
    private static partial Regex RagQuestionPattern();

    [GeneratedRegex(@"(?is)\bContext:\s*(?<context>.*?)\s*\bQuestion:")]
    private static partial Regex RagContextPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}
