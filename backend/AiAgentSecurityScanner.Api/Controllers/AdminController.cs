using AiAgentSecurityScanner.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiAgentSecurityScanner.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminController(
    AppDbContext dbContext,
    IHostEnvironment environment,
    ILogger<AdminController> logger) : ControllerBase
{
    private const string RequiredConfirmation = "RESET DEMO";

    [HttpPost("reset-demo")]
    public async Task<ActionResult<ResetDemoResponse>> ResetDemo(
        ResetDemoRequest request,
        CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Demo environment reset is only available in Development."
            });
        }

        if (!string.Equals(request.Confirmation, RequiredConfirmation, StringComparison.Ordinal))
        {
            return BadRequest(new
            {
                message = $"Type {RequiredConfirmation} to confirm the demo reset."
            });
        }

        var semanticCacheEntries = await dbContext.SemanticCacheEntries.CountAsync(cancellationToken);
        var chatMessages = await dbContext.ChatMessages.CountAsync(cancellationToken);
        var chatSessions = await dbContext.ChatSessions.CountAsync(cancellationToken);
        var sensitiveDataFindings = await dbContext.SensitiveDataFindings.CountAsync(cancellationToken);
        var requestLogs = await dbContext.ChatRequestLogs.CountAsync(cancellationToken);

        dbContext.SemanticCacheEntries.RemoveRange(await dbContext.SemanticCacheEntries.ToListAsync(cancellationToken));
        dbContext.ChatMessages.RemoveRange(await dbContext.ChatMessages.ToListAsync(cancellationToken));
        dbContext.ChatSessions.RemoveRange(await dbContext.ChatSessions.ToListAsync(cancellationToken));
        dbContext.SensitiveDataFindings.RemoveRange(await dbContext.SensitiveDataFindings.ToListAsync(cancellationToken));
        dbContext.ChatRequestLogs.RemoveRange(await dbContext.ChatRequestLogs.ToListAsync(cancellationToken));

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogWarning(
            "Developer demo reset cleared {SemanticCacheEntries} semantic cache entries, {ChatSessions} chat sessions, {ChatMessages} chat messages, {RequestLogs} request logs, and {SensitiveDataFindings} sensitive data findings.",
            semanticCacheEntries,
            chatSessions,
            chatMessages,
            requestLogs,
            sensitiveDataFindings);

        return Ok(new ResetDemoResponse(
            semanticCacheEntries,
            chatSessions,
            chatMessages,
            requestLogs,
            sensitiveDataFindings,
            "Security Gateway demo data cleared. Applications, policies, and users were preserved."));
    }

    [HttpPost("clear-semantic-cache")]
    public async Task<ActionResult<ClearSemanticCacheResponse>> ClearSemanticCache(
        ResetDemoRequest request,
        CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Semantic cache clear is only available in Development."
            });
        }

        if (!string.Equals(request.Confirmation, RequiredConfirmation, StringComparison.Ordinal))
        {
            return BadRequest(new
            {
                message = $"Type {RequiredConfirmation} to confirm the semantic cache clear."
            });
        }

        var semanticCacheEntries = await dbContext.SemanticCacheEntries.CountAsync(cancellationToken);
        dbContext.SemanticCacheEntries.RemoveRange(await dbContext.SemanticCacheEntries.ToListAsync(cancellationToken));
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogWarning(
            "Developer semantic cache clear removed {SemanticCacheEntries} semantic cache entries.",
            semanticCacheEntries);

        return Ok(new ClearSemanticCacheResponse(
            semanticCacheEntries,
            "Security Gateway semantic cache cleared."));
    }
}

public sealed record ResetDemoRequest(string Confirmation);

public sealed record ResetDemoResponse(
    int SemanticCacheEntriesCleared,
    int ChatSessionsCleared,
    int ChatMessagesCleared,
    int RequestLogsCleared,
    int SensitiveDataFindingsCleared,
    string Message);

public sealed record ClearSemanticCacheResponse(
    int SemanticCacheEntriesCleared,
    string Message);
