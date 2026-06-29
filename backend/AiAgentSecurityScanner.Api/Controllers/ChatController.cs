using AiAgentSecurityScanner.Api.Data;
using AiAgentSecurityScanner.Api.DTOs;
using AiAgentSecurityScanner.Api.Models;
using AiAgentSecurityScanner.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiAgentSecurityScanner.Api.Controllers;

[ApiController]
[Route("api/chat")]
public class ChatController(IChatGatewayService chatGateway, AppDbContext db, ILogger<ChatController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ChatResponse>> Send(ChatRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "Message is required." });

        ChatResponse response;
        try
        {
            response = await chatGateway.SendAsync(
                request,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                cancellationToken);
        }
        catch (ApplicationValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        logger.LogInformation(
            "Chat API returning response. RequestId={RequestId} ResponseLengthReturnedByApi={ResponseLengthReturnedByApi} CacheHit={CacheHit}",
            response.RequestId,
            response.Response.Length,
            response.CacheHit);

        return Ok(response);
    }

    [HttpGet("history")]
    public async Task<ActionResult<IReadOnlyList<ChatHistoryItemDto>>> History(CancellationToken cancellationToken)
    {
        var rows = await db.ChatRequestLogs
            .AsNoTracking()
            .OrderByDescending(x => x.TimestampUtc)
            .Take(50)
            .Select(x => new
            {
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
                LlmTokensUsed = x.LlmTokensUsed > 0 ? x.LlmTokensUsed : (!x.CacheHit ? (x.TotalTokens ?? 0) : 0),
                TokensSaved = x.TokensSaved > 0 ? x.TokensSaved : (x.CacheHit ? (x.PromptTokens ?? 0) + (x.ResponseTokens ?? 0) : 0),
                CachedTokensServed = x.CachedTokensServed > 0 ? x.CachedTokensServed : (x.CacheHit ? (x.PromptTokens ?? 0) + (x.ResponseTokens ?? 0) : 0),
                x.CacheHit,
                x.ExactCacheHit,
                x.SemanticCacheHit,
                x.SimilarityScore,
                x.ErrorMessage,
                x.MatchedPolicyName,
                FindingCount = x.Findings.Count
            })
            .ToListAsync(cancellationToken);

        var items = rows
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

        return Ok(items);
    }

    [HttpGet("sessions")]
    public async Task<ActionResult<IReadOnlyList<ChatSessionDto>>> Sessions(CancellationToken cancellationToken)
    {
        var rows = await db.ChatSessions
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Select(x => new { x.Id, x.Title, x.CreatedAtUtc, x.UpdatedAtUtc })
            .ToListAsync(cancellationToken);

        var sessions = rows
            .Select(x => new ChatSessionDto(x.Id, x.Title, UtcDateTime.FromStorage(x.CreatedAtUtc), UtcDateTime.FromStorage(x.UpdatedAtUtc)))
            .ToList();

        return Ok(sessions);
    }

    [HttpPost("sessions")]
    public async Task<ActionResult<ChatSessionDto>> CreateSession(CreateChatSessionRequest request, CancellationToken cancellationToken)
    {
        var session = new ChatSession
        {
            Title = string.IsNullOrWhiteSpace(request.Title) ? "New chat" : request.Title.Trim()
        };
        db.ChatSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new ChatSessionDto(session.Id, session.Title, UtcDateTime.FromStorage(session.CreatedAtUtc), UtcDateTime.FromStorage(session.UpdatedAtUtc)));
    }

    [HttpGet("sessions/{id:guid}/messages")]
    public async Task<ActionResult<IReadOnlyList<ChatMessageDto>>> SessionMessages(Guid id, CancellationToken cancellationToken)
    {
        var rows = await db.ChatMessages
            .AsNoTracking()
            .Where(x => x.SessionId == id)
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.SessionId,
                x.Role,
                x.Content,
                x.CreatedAtUtc,
                x.Model,
                x.PolicyDecision,
                x.RiskScore,
                x.RequestId
            })
            .ToListAsync(cancellationToken);

        var messages = rows
            .Select(x => new ChatMessageDto(
                x.Id,
                x.SessionId,
                x.Role,
                x.Content,
                UtcDateTime.FromStorage(x.CreatedAtUtc),
                x.Model,
                x.PolicyDecision,
                x.RiskScore,
                x.RequestId))
            .ToList();

        return Ok(messages);
    }
}
