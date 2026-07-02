using System.Text.Json;
using System.Diagnostics;
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
    Task<SemanticCacheLookupResult> LookupAsync(string prompt, string provider, string model, string policyDecision, ILLMProvider llmProvider, CancellationToken cancellationToken);
    Task SaveAsync(string originalPrompt, string normalizedPrompt, float[]? embedding, string response, string provider, string model, string policyDecision, int? promptTokens, int? responseTokens, int? totalTokens, CancellationToken cancellationToken);
}

public class SemanticCacheService(
    AppDbContext db,
    IPromptNormalizer normalizer,
    IOptions<SemanticCacheOptions> options,
    ILogger<SemanticCacheService> logger) : ISemanticCacheService
{
    private const int LegacyTruncatedResponseTokenLimit = 120;

    public async Task<SemanticCacheLookupResult> LookupAsync(string prompt, string provider, string model, string policyDecision, ILLMProvider llmProvider, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var normalized = normalizer.Normalize(prompt);
        var cacheModelScope = BuildCacheModelScope(provider, model);
        var cacheKey = $"{cacheModelScope}|{policyDecision}|{normalized}";
        if (!options.Value.Enabled)
        {
            stopwatch.Stop();
            logger.LogInformation("Semantic cache lookup disabled. Provider={Provider} Model={Model} PolicyDecision={PolicyDecision} DurationMs={DurationMs}", provider, model, policyDecision, stopwatch.ElapsedMilliseconds);
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
            logger.LogInformation("Semantic cache lookup complete. Result=ExactHit Provider={Provider} Model={Model} PolicyDecision={PolicyDecision} DurationMs={DurationMs} CachedResponseLength={CachedResponseLength}", provider, model, policyDecision, stopwatch.ElapsedMilliseconds, exact.CachedResponse.Length);
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
            logger.LogInformation("Semantic cache lookup complete. Result=MissNoEmbedding Provider={Provider} Model={Model} PolicyDecision={PolicyDecision} DurationMs={DurationMs}", provider, model, policyDecision, stopwatch.ElapsedMilliseconds);
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
            logger.LogInformation("Semantic cache lookup complete. Result=SemanticHit Provider={Provider} Model={Model} PolicyDecision={PolicyDecision} DurationMs={DurationMs} Similarity={Similarity} CandidateCount={CandidateCount} SkippedTruncatedCandidates={SkippedTruncatedCandidates} CachedResponseLength={CachedResponseLength}", provider, model, policyDecision, stopwatch.ElapsedMilliseconds, bestScore, candidates.Count, skippedTruncatedCandidates, best.CachedResponse.Length);
            return new(true, false, true, bestScore, best.CachedResponse, best.PromptTokens, best.ResponseTokens, best.TotalTokens, cacheKey, best.OriginalPrompt, normalized, embedding);
        }

        stopwatch.Stop();
        logger.LogInformation("Semantic cache lookup complete. Result=Miss Provider={Provider} Model={Model} PolicyDecision={PolicyDecision} DurationMs={DurationMs} BestSimilarity={Similarity} CandidateCount={CandidateCount} SkippedTruncatedCandidates={SkippedTruncatedCandidates}", provider, model, policyDecision, stopwatch.ElapsedMilliseconds, best is null ? null : bestScore, candidates.Count, skippedTruncatedCandidates);
        return new(false, false, false, best is null ? null : bestScore, null, null, null, null, cacheKey, best?.OriginalPrompt, normalized, embedding);
    }

    public Task SaveAsync(string originalPrompt, string normalizedPrompt, float[]? embedding, string response, string provider, string model, string policyDecision, int? promptTokens, int? responseTokens, int? totalTokens, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled) return Task.CompletedTask;
        if (embedding is null || embedding.Length == 0) return Task.CompletedTask;

        logger.LogInformation(
            "Saving complete response to semantic cache. Provider={Provider} Model={Model} PolicyDecision={PolicyDecision} ResponseLengthBeforeCaching={ResponseLengthBeforeCaching}",
            provider,
            model,
            policyDecision,
            response.Length);

        db.SemanticCacheEntries.Add(new SemanticCacheEntry
        {
            OriginalPrompt = originalPrompt,
            NormalizedPrompt = normalizedPrompt,
            EmbeddingJson = JsonSerializer.Serialize(embedding),
            CachedResponse = response,
            Model = BuildCacheModelScope(provider, model),
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

    private static string BuildCacheModelScope(string provider, string model)
    {
        return $"{provider.Trim().ToLowerInvariant()}:{model.Trim().ToLowerInvariant()}";
    }
}
