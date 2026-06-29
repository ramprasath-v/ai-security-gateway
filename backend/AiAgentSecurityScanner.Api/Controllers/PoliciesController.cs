using AiAgentSecurityScanner.Api.Data;
using AiAgentSecurityScanner.Api.DTOs;
using AiAgentSecurityScanner.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiAgentSecurityScanner.Api.Controllers;

[ApiController]
[Route("api/policies")]
public class PoliciesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SecurityPolicyDto>>> Get(CancellationToken cancellationToken)
    {
        var rows = await db.SecurityPolicies
            .AsNoTracking()
            .OrderByDescending(x => x.Enabled)
            .ThenBy(x => x.FindingType)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return Ok(rows.Select(ToDto).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<SecurityPolicyDto>> Create(SecurityPolicyRequest request, CancellationToken cancellationToken)
    {
        var validationError = Validate(request);
        if (validationError is not null) return BadRequest(new { error = validationError });

        var now = DateTime.UtcNow;
        var policy = new SecurityPolicy
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            FindingType = request.FindingType.Trim(),
            Action = NormalizeAction(request.Action),
            RiskScore = Math.Clamp(request.RiskScore, 0, 100),
            Enabled = request.Enabled,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        db.SecurityPolicies.Add(policy);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(policy));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SecurityPolicyDto>> Update(Guid id, SecurityPolicyRequest request, CancellationToken cancellationToken)
    {
        var validationError = Validate(request);
        if (validationError is not null) return BadRequest(new { error = validationError });

        var policy = await db.SecurityPolicies.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (policy is null) return NotFound(new { error = "Policy not found." });

        policy.Name = request.Name.Trim();
        policy.Description = request.Description?.Trim() ?? string.Empty;
        policy.FindingType = request.FindingType.Trim();
        policy.Action = NormalizeAction(request.Action);
        policy.RiskScore = Math.Clamp(request.RiskScore, 0, 100);
        policy.Enabled = request.Enabled;
        policy.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(policy));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var policy = await db.SecurityPolicies.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (policy is null) return NotFound(new { error = "Policy not found." });

        db.SecurityPolicies.Remove(policy);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static string? Validate(SecurityPolicyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return "Policy name is required.";
        if (string.IsNullOrWhiteSpace(request.FindingType)) return "Finding type is required.";
        if (string.IsNullOrWhiteSpace(request.Action)) return "Action is required.";
        if (!IsValidAction(request.Action)) return "Action must be Allow, Mask, or Block.";
        return null;
    }

    private static bool IsValidAction(string action)
    {
        return action.Trim().Equals("Allow", StringComparison.OrdinalIgnoreCase) ||
               action.Trim().Equals("Mask", StringComparison.OrdinalIgnoreCase) ||
               action.Trim().Equals("Block", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeAction(string action)
    {
        return action.Trim().ToLowerInvariant() switch
        {
            "block" => "Block",
            "mask" => "Mask",
            _ => "Allow"
        };
    }

    private static SecurityPolicyDto ToDto(SecurityPolicy policy)
    {
        return new SecurityPolicyDto(
            policy.Id,
            policy.Name,
            policy.Description,
            policy.FindingType,
            policy.Action,
            policy.RiskScore,
            policy.Enabled,
            UtcDateTime.FromStorage(policy.CreatedAtUtc),
            UtcDateTime.FromStorage(policy.UpdatedAtUtc));
    }
}
