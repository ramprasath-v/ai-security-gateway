using System.Text.RegularExpressions;
using AiAgentSecurityScanner.Api.Data;
using AiAgentSecurityScanner.Api.DTOs;
using AiAgentSecurityScanner.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiAgentSecurityScanner.Api.Controllers;

[ApiController]
[Route("api/applications")]
public partial class ApplicationsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AiApplicationDto>>> Get(CancellationToken cancellationToken)
    {
        var rows = await db.AiApplications
            .AsNoTracking()
            .OrderByDescending(x => x.Enabled)
            .ThenBy(x => x.Environment)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return Ok(rows.Select(ToDto).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<AiApplicationDto>> Create(AiApplicationRequest request, CancellationToken cancellationToken)
    {
        var validationError = Validate(request);
        if (validationError is not null) return BadRequest(new { error = validationError });

        var appKey = string.IsNullOrWhiteSpace(request.AppKey)
            ? BuildAppKey(request.Name)
            : request.AppKey.Trim();
        if (await db.AiApplications.AnyAsync(x => x.AppKey == appKey, cancellationToken))
            return BadRequest(new { error = "Application key already exists." });

        var now = DateTime.UtcNow;
        var app = new AiApplication
        {
            AppKey = appKey,
            Name = request.Name.Trim(),
            Owner = request.Owner.Trim(),
            Environment = NormalizeEnvironment(request.Environment),
            Description = request.Description?.Trim() ?? string.Empty,
            Enabled = request.Enabled,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        db.AiApplications.Add(app);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(app));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AiApplicationDto>> Update(Guid id, AiApplicationRequest request, CancellationToken cancellationToken)
    {
        var validationError = Validate(request);
        if (validationError is not null) return BadRequest(new { error = validationError });

        var app = await db.AiApplications.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (app is null) return NotFound(new { error = "Application not found." });

        var appKey = string.IsNullOrWhiteSpace(request.AppKey)
            ? app.AppKey
            : request.AppKey.Trim();
        if (await db.AiApplications.AnyAsync(x => x.Id != id && x.AppKey == appKey, cancellationToken))
            return BadRequest(new { error = "Application key already exists." });

        app.AppKey = appKey;
        app.Name = request.Name.Trim();
        app.Owner = request.Owner.Trim();
        app.Environment = NormalizeEnvironment(request.Environment);
        app.Description = request.Description?.Trim() ?? string.Empty;
        app.Enabled = request.Enabled;
        app.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(app));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var app = await db.AiApplications.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (app is null) return NotFound(new { error = "Application not found." });

        db.AiApplications.Remove(app);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static string? Validate(AiApplicationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return "Application name is required.";
        if (string.IsNullOrWhiteSpace(request.Owner)) return "Owner is required.";
        if (string.IsNullOrWhiteSpace(request.Environment)) return "Environment is required.";
        if (!IsValidEnvironment(request.Environment)) return "Environment must be Dev, Test, or Prod.";
        return null;
    }

    private static bool IsValidEnvironment(string environment)
    {
        var value = environment.Trim().ToLowerInvariant();
        return value is "dev" or "development" or "test" or "qa" or "prod" or "production";
    }

    private static string NormalizeEnvironment(string environment)
    {
        return environment.Trim().ToLowerInvariant() switch
        {
            "prod" or "production" => "Prod",
            "test" or "qa" => "Test",
            _ => "Dev"
        };
    }

    private static string BuildAppKey(string name)
    {
        var normalized = AppKeyPattern().Replace(name.Trim().ToLowerInvariant(), "-").Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? $"app-{Guid.NewGuid():N}"[..16] : normalized;
    }

    private static AiApplicationDto ToDto(AiApplication app)
    {
        return new AiApplicationDto(
            app.Id,
            app.AppKey,
            app.Name,
            app.Owner,
            app.Environment,
            app.Description,
            app.Enabled,
            UtcDateTime.FromStorage(app.CreatedAtUtc),
            UtcDateTime.FromStorage(app.UpdatedAtUtc));
    }

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex AppKeyPattern();
}
