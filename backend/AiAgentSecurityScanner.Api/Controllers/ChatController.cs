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

        var response = await chatGateway.SendAsync(
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

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
        var items = await db.ChatRequestLogs
            .AsNoTracking()
            .OrderByDescending(x => x.TimestampUtc)
            .Take(50)
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

        return Ok(items);
    }

    [HttpGet("sessions")]
    public async Task<ActionResult<IReadOnlyList<ChatSessionDto>>> Sessions(CancellationToken cancellationToken)
    {
        var sessions = await db.ChatSessions
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Select(x => new ChatSessionDto(x.Id, x.Title, x.CreatedAtUtc, x.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

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
        return Ok(new ChatSessionDto(session.Id, session.Title, session.CreatedAtUtc, session.UpdatedAtUtc));
    }

    [HttpGet("sessions/{id:guid}/messages")]
    public async Task<ActionResult<IReadOnlyList<ChatMessageDto>>> SessionMessages(Guid id, CancellationToken cancellationToken)
    {
        var messages = await db.ChatMessages
            .AsNoTracking()
            .Where(x => x.SessionId == id)
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => new ChatMessageDto(
                x.Id,
                x.SessionId,
                x.Role,
                x.Content,
                x.CreatedAtUtc,
                x.Model,
                x.PolicyDecision,
                x.RiskScore,
                x.RequestId))
            .ToListAsync(cancellationToken);

        return Ok(messages);
    }
}
