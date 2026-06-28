using System.Text.Json;
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
    string NormalizedPrompt,
    float[]? Embedding);

public interface ISemanticCacheService
{
    Task<SemanticCacheLookupResult> LookupAsync(string prompt, string model, string policyDecision, CancellationToken cancellationToken);
    Task SaveAsync(string originalPrompt, string normalizedPrompt, float[]? embedding, string response, string model, string policyDecision, int? promptTokens, int? responseTokens, int? totalTokens, CancellationToken cancellationToken);
}

public class SemanticCacheService(
    AppDbContext db,
    IPromptNormalizer normalizer,
    IOllamaEmbeddingService embeddingService,
    IOptions<SemanticCacheOptions> options,
    ILogger<SemanticCacheService> logger) : ISemanticCacheService
{
    public async Task<SemanticCacheLookupResult> LookupAsync(string prompt, string model, string policyDecision, CancellationToken cancellationToken)
    {
        var normalized = normalizer.Normalize(prompt);
        var cacheKey = $"{model.Trim().ToLowerInvariant()}|{policyDecision}|{normalized}";
        if (!options.Value.Enabled)
            return new(false, false, false, null, null, null, null, null, cacheKey, normalized, null);

        var exact = await db.SemanticCacheEntries
            .AsNoTracking()
            .Where(x => x.Model == model && x.PolicyDecision == policyDecision && x.NormalizedPrompt == normalized)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (exact is not null)
        {
            logger.LogInformation("Exact cache hit. Model={Model} PolicyDecision={PolicyDecision}", model, policyDecision);
            return new(true, true, false, 1.0, exact.CachedResponse, exact.PromptTokens, exact.ResponseTokens, exact.TotalTokens, cacheKey, normalized, null);
        }

        var embedding = await embeddingService.GenerateEmbeddingAsync(normalized, cancellationToken);
        if (embedding is null || embedding.Length == 0)
            return new(false, false, false, null, null, null, null, null, cacheKey, normalized, null);

        var candidates = await db.SemanticCacheEntries
            .AsNoTracking()
            .Where(x => x.Model == model && x.PolicyDecision == policyDecision)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(250)
            .ToListAsync(cancellationToken);

        SemanticCacheEntry? best = null;
        double bestScore = 0;
        foreach (var candidate in candidates)
        {
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
            logger.LogInformation("Semantic cache hit. Model={Model} PolicyDecision={PolicyDecision} Similarity={Similarity}", model, policyDecision, bestScore);
            return new(true, false, true, bestScore, best.CachedResponse, best.PromptTokens, best.ResponseTokens, best.TotalTokens, cacheKey, normalized, embedding);
        }

        logger.LogInformation("Cache miss. Model={Model} PolicyDecision={PolicyDecision} BestSimilarity={Similarity}", model, policyDecision, best is null ? null : bestScore);
        return new(false, false, false, best is null ? null : bestScore, null, null, null, null, cacheKey, normalized, embedding);
    }

    public async Task SaveAsync(string originalPrompt, string normalizedPrompt, float[]? embedding, string response, string model, string policyDecision, int? promptTokens, int? responseTokens, int? totalTokens, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled) return;
        embedding ??= await embeddingService.GenerateEmbeddingAsync(normalizedPrompt, cancellationToken);
        if (embedding is null || embedding.Length == 0) return;

        db.SemanticCacheEntries.Add(new SemanticCacheEntry
        {
            OriginalPrompt = originalPrompt,
            NormalizedPrompt = normalizedPrompt,
            EmbeddingJson = JsonSerializer.Serialize(embedding),
            CachedResponse = response,
            Model = model,
            PolicyDecision = policyDecision,
            PromptTokens = promptTokens,
            ResponseTokens = responseTokens,
            TotalTokens = totalTokens
        });
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
}
