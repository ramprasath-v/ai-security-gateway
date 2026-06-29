using AiAgentSecurityScanner.Api.Data;
using AiAgentSecurityScanner.Api.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiAgentSecurityScanner.Api.Controllers;

[ApiController, Route("api/dashboard")]
public class DashboardController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DashboardDto>> Get(CancellationToken cancellationToken)
    {
        var recent = ToHistoryItems(await db.ChatRequestLogs
            .AsNoTracking()
            .OrderByDescending(x => x.TimestampUtc)
            .Take(10)
            .Select(x => new HistoryRow(
                x.Id,
                x.TimestampUtc,
                x.ApplicationId,
                x.ApplicationKey,
                x.ApplicationName,
                x.ApplicationOwner,
                x.ApplicationEnvironment,
                x.Provider,
                x.Model,
                x.Status,
                x.PolicyAction,
                x.RiskScore,
                x.PromptPreview,
                x.ResponsePreview,
                x.TotalDurationMs,
                x.OllamaDurationMs,
                x.PromptLength,
                x.ResponseLength,
                x.PromptTokens,
                x.ResponseTokens,
                x.TotalTokens,
                x.LlmTokensUsed > 0 ? x.LlmTokensUsed : (!x.CacheHit ? (x.TotalTokens ?? 0) : 0),
                x.TokensSaved > 0 ? x.TokensSaved : (x.CacheHit ? (x.PromptTokens ?? 0) + (x.ResponseTokens ?? 0) : 0),
                x.CachedTokensServed > 0 ? x.CachedTokensServed : (x.CacheHit ? (x.PromptTokens ?? 0) + (x.ResponseTokens ?? 0) : 0),
                x.CacheHit,
                x.ExactCacheHit,
                x.SemanticCacheHit,
                x.SimilarityScore,
                x.ErrorMessage,
                x.MatchedPolicyName,
                x.Findings.Count))
            .ToListAsync(cancellationToken));

        var recentHighRisk = ToHistoryItems(await db.ChatRequestLogs
            .AsNoTracking()
            .Where(x => x.RiskScore >= 60)
            .OrderByDescending(x => x.RiskScore)
            .ThenByDescending(x => x.TimestampUtc)
            .Take(5)
            .Select(x => new HistoryRow(
                x.Id,
                x.TimestampUtc,
                x.ApplicationId,
                x.ApplicationKey,
                x.ApplicationName,
                x.ApplicationOwner,
                x.ApplicationEnvironment,
                x.Provider,
                x.Model,
                x.Status,
                x.PolicyAction,
                x.RiskScore,
                x.PromptPreview,
                x.ResponsePreview,
                x.TotalDurationMs,
                x.OllamaDurationMs,
                x.PromptLength,
                x.ResponseLength,
                x.PromptTokens,
                x.ResponseTokens,
                x.TotalTokens,
                x.LlmTokensUsed > 0 ? x.LlmTokensUsed : (!x.CacheHit ? (x.TotalTokens ?? 0) : 0),
                x.TokensSaved > 0 ? x.TokensSaved : (x.CacheHit ? (x.PromptTokens ?? 0) + (x.ResponseTokens ?? 0) : 0),
                x.CachedTokensServed > 0 ? x.CachedTokensServed : (x.CacheHit ? (x.PromptTokens ?? 0) + (x.ResponseTokens ?? 0) : 0),
                x.CacheHit,
                x.ExactCacheHit,
                x.SemanticCacheHit,
                x.SimilarityScore,
                x.ErrorMessage,
                x.MatchedPolicyName,
                x.Findings.Count))
            .ToListAsync(cancellationToken));

        var modelUsage = (await db.ChatRequestLogs
                .GroupBy(x => x.Model)
                .Select(x => new { Model = x.Key, Count = x.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync(cancellationToken))
            .Select(x => new ModelUsageDto(x.Model, x.Count))
            .ToList();

        var providerUsage = (await db.ChatRequestLogs
                .GroupBy(x => x.Provider)
                .Select(x => new { Provider = x.Key, Count = x.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync(cancellationToken))
            .Select(x => new ProviderUsageDto(x.Provider, x.Count))
            .ToList();

        var averageDurationByProvider = (await db.ChatRequestLogs
                .Where(x => x.TotalDurationMs != null)
                .GroupBy(x => x.Provider)
                .Select(x => new { Provider = x.Key, AverageDurationMs = x.Average(y => (double)y.TotalDurationMs!.Value) })
                .OrderByDescending(x => x.AverageDurationMs)
                .ToListAsync(cancellationToken))
            .Select(x => new ProviderDurationDto(x.Provider, x.AverageDurationMs))
            .ToList();

        var tokensByProvider = (await db.ChatRequestLogs
                .GroupBy(x => x.Provider)
                .Select(x => new
                {
                    Provider = x.Key,
                    LlmTokensUsed = x.Sum(y => (long)(y.LlmTokensUsed > 0 ? y.LlmTokensUsed : (!y.CacheHit ? (y.TotalTokens ?? 0) : 0))),
                    TokensSaved = x.Sum(y => (long)(y.TokensSaved > 0 ? y.TokensSaved : (y.CacheHit ? (y.PromptTokens ?? 0) + (y.ResponseTokens ?? 0) : 0)))
                })
                .OrderByDescending(x => x.LlmTokensUsed)
                .ToListAsync(cancellationToken))
            .Select(x => new ProviderTokensDto(x.Provider, x.LlmTokensUsed, x.TokensSaved))
            .ToList();

        var applicationUsage = (await db.ChatRequestLogs
                .GroupBy(x => new { Name = x.ApplicationName ?? "Unknown", Environment = x.ApplicationEnvironment ?? "Unknown" })
                .Select(x => new { x.Key.Name, x.Key.Environment, Count = x.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync(cancellationToken))
            .Select(x => new ApplicationUsageDto(x.Name, x.Environment, x.Count))
            .ToList();

        var violationsByApplication = (await db.ChatRequestLogs
                .Where(x => x.PolicyAction != "allow")
                .GroupBy(x => new { Name = x.ApplicationName ?? "Unknown", Environment = x.ApplicationEnvironment ?? "Unknown" })
                .Select(x => new { x.Key.Name, x.Key.Environment, Count = x.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync(cancellationToken))
            .Select(x => new ApplicationViolationDto(x.Name, x.Environment, x.Count))
            .ToList();

        var tokensByApplication = (await db.ChatRequestLogs
                .GroupBy(x => new { Name = x.ApplicationName ?? "Unknown", Environment = x.ApplicationEnvironment ?? "Unknown" })
                .Select(x => new
                {
                    x.Key.Name,
                    x.Key.Environment,
                    LlmTokensUsed = x.Sum(y => (long)(y.LlmTokensUsed > 0 ? y.LlmTokensUsed : (!y.CacheHit ? (y.TotalTokens ?? 0) : 0))),
                    TokensSaved = x.Sum(y => (long)(y.TokensSaved > 0 ? y.TokensSaved : (y.CacheHit ? (y.PromptTokens ?? 0) + (y.ResponseTokens ?? 0) : 0)))
                })
                .OrderByDescending(x => x.LlmTokensUsed)
                .ToListAsync(cancellationToken))
            .Select(x => new ApplicationTokensDto(x.Name, x.Environment, x.LlmTokensUsed, x.TokensSaved))
            .ToList();

        var averageDurationByApplication = (await db.ChatRequestLogs
                .Where(x => x.TotalDurationMs != null)
                .GroupBy(x => new { Name = x.ApplicationName ?? "Unknown", Environment = x.ApplicationEnvironment ?? "Unknown" })
                .Select(x => new { x.Key.Name, x.Key.Environment, AverageDurationMs = x.Average(y => (double)y.TotalDurationMs!.Value) })
                .OrderByDescending(x => x.AverageDurationMs)
                .ToListAsync(cancellationToken))
            .Select(x => new ApplicationDurationDto(x.Name, x.Environment, x.AverageDurationMs))
            .ToList();

        var policyActions = (await db.ChatRequestLogs
                .GroupBy(x => x.PolicyAction)
                .Select(x => new { Action = x.Key, Count = x.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync(cancellationToken))
            .Select(x => new PolicyActionCountDto(x.Action, x.Count))
            .ToList();

        var findingTypes = (await db.SensitiveDataFindings
                .GroupBy(x => x.Type)
                .Select(x => new { Type = x.Key, Count = x.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync(cancellationToken))
            .Select(x => new SensitiveFindingCountDto(x.Type, x.Count))
            .ToList();

        var lastRequestTime = await db.ChatRequestLogs
            .OrderByDescending(x => x.TimestampUtc)
            .Select(x => (DateTime?)x.TimestampUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var dashboard = new DashboardDto(
            await db.ChatRequestLogs.CountAsync(cancellationToken),
            await db.ChatRequestLogs.CountAsync(x => x.Status == "Completed", cancellationToken),
            await db.ChatRequestLogs.CountAsync(x => x.Status == "Blocked", cancellationToken),
            await db.ChatRequestLogs.CountAsync(x => x.PolicyAction != "allow", cancellationToken),
            await db.SensitiveDataFindings.CountAsync(cancellationToken),
            await db.ChatRequestLogs
                .Where(x => x.TotalDurationMs != null)
                .Select(x => (double)x.TotalDurationMs!.Value)
                .DefaultIfEmpty()
                .AverageAsync(cancellationToken),
            await db.ChatRequestLogs
                .Where(x => x.TotalDurationMs != null)
                .MinAsync(x => (long?)x.TotalDurationMs, cancellationToken),
            await db.ChatRequestLogs
                .Where(x => x.TotalDurationMs != null)
                .MaxAsync(x => (long?)x.TotalDurationMs, cancellationToken),
            UtcDateTime.FromStorage(lastRequestTime),
            await db.ChatRequestLogs
                .SumAsync(x => (long?)(x.LlmTokensUsed > 0 ? x.LlmTokensUsed : (!x.CacheHit ? (x.TotalTokens ?? 0) : 0)), cancellationToken) ?? 0,
            await db.ChatRequestLogs
                .SumAsync(x => (long?)(x.TokensSaved > 0 ? x.TokensSaved : (x.CacheHit ? (x.PromptTokens ?? 0) + (x.ResponseTokens ?? 0) : 0)), cancellationToken) ?? 0,
            await db.ChatRequestLogs
                .SumAsync(x => (long?)(x.CachedTokensServed > 0 ? x.CachedTokensServed : (x.CacheHit ? (x.PromptTokens ?? 0) + (x.ResponseTokens ?? 0) : 0)), cancellationToken) ?? 0,
            await db.ChatRequestLogs
                .Where(x => !x.CacheHit && (x.LlmTokensUsed > 0 || x.TotalTokens != null))
                .Select(x => (double)(x.LlmTokensUsed > 0 ? x.LlmTokensUsed : (x.TotalTokens ?? 0)))
                .DefaultIfEmpty()
                .AverageAsync(cancellationToken),
            await db.ChatRequestLogs.CountAsync(x => x.CacheHit, cancellationToken),
            await db.ChatRequestLogs.CountAsync(x => x.ExactCacheHit, cancellationToken),
            await db.ChatRequestLogs.CountAsync(x => x.SemanticCacheHit, cancellationToken),
            await db.ChatRequestLogs.CountAsync(x => !x.CacheHit && !x.WasBlockedBeforeModelCall, cancellationToken),
            await db.ChatRequestLogs.CountAsync(x => x.PolicyAction == "block" && x.Findings.Any(f =>
                f.Type == "Secret" ||
                f.Type == "OpenAI API Key" ||
                f.Type == "AWS Access Key" ||
                f.Type == "GitHub Token" ||
                f.Type == "JWT" ||
                f.Type == "Database Connection String" ||
                f.Type == "Private Key"), cancellationToken),
            providerUsage,
            averageDurationByProvider,
            tokensByProvider,
            applicationUsage,
            violationsByApplication,
            tokensByApplication,
            averageDurationByApplication,
            modelUsage,
            policyActions,
            findingTypes,
            recentHighRisk,
            recent);

        return Ok(dashboard);
    }

    private static IReadOnlyList<ChatHistoryItemDto> ToHistoryItems(IEnumerable<HistoryRow> rows)
    {
        return rows
            .Select(x => new ChatHistoryItemDto(
                x.Id,
                UtcDateTime.FromStorage(x.TimestampUtc),
                x.ApplicationId,
                x.ApplicationKey,
                x.ApplicationName,
                x.ApplicationOwner,
                x.ApplicationEnvironment,
                x.Provider,
                x.Model,
                x.Status,
                x.PolicyAction,
                x.RiskScore,
                x.PromptPreview,
                x.ResponsePreview,
                x.TotalDurationMs,
                x.OllamaDurationMs,
                x.PromptLength,
                x.ResponseLength,
                x.PromptTokens,
                x.ResponseTokens,
                x.TotalTokens,
                x.LlmTokensUsed,
                x.TokensSaved,
                x.CachedTokensServed,
                x.CacheHit,
                x.ExactCacheHit,
                x.SemanticCacheHit,
                x.SimilarityScore,
                x.ErrorMessage,
                x.MatchedPolicyName,
                x.FindingCount))
            .ToList();
    }

    private record HistoryRow(
        Guid Id,
        DateTime TimestampUtc,
        Guid? ApplicationId,
        string? ApplicationKey,
        string? ApplicationName,
        string? ApplicationOwner,
        string? ApplicationEnvironment,
        string Provider,
        string Model,
        string Status,
        string PolicyAction,
        int RiskScore,
        string PromptPreview,
        string? ResponsePreview,
        long? TotalDurationMs,
        long? OllamaDurationMs,
        int PromptLength,
        int ResponseLength,
        int? PromptTokens,
        int? ResponseTokens,
        int? TotalTokens,
        int LlmTokensUsed,
        int TokensSaved,
        int CachedTokensServed,
        bool CacheHit,
        bool ExactCacheHit,
        bool SemanticCacheHit,
        double? SimilarityScore,
        string? ErrorMessage,
        string? MatchedPolicyName,
        int FindingCount);
}
