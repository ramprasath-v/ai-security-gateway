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
        var recent = await db.ChatRequestLogs
            .AsNoTracking()
            .OrderByDescending(x => x.TimestampUtc)
            .Take(10)
            .Select(x => new ChatHistoryItemDto(
                x.Id,
                x.TimestampUtc,
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
                x.Findings.Count))
            .ToListAsync(cancellationToken);

        var modelUsage = (await db.ChatRequestLogs
                .GroupBy(x => x.Model)
                .Select(x => new { Model = x.Key, Count = x.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync(cancellationToken))
            .Select(x => new ModelUsageDto(x.Model, x.Count))
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
            await db.ChatRequestLogs
                .OrderByDescending(x => x.TimestampUtc)
                .Select(x => (DateTime?)x.TimestampUtc)
                .FirstOrDefaultAsync(cancellationToken),
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
            modelUsage,
            policyActions,
            findingTypes,
            recent);

        return Ok(dashboard);
    }
}
