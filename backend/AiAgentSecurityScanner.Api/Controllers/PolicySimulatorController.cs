using AiAgentSecurityScanner.Api.DTOs;
using AiAgentSecurityScanner.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AiAgentSecurityScanner.Api.Controllers;

[ApiController]
[Route("api/policy-simulator")]
public class PolicySimulatorController(
    IAiSecurityDetectionService detectionService,
    IPolicyService policyService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<PolicySimulationResponse>> Simulate(PolicySimulationRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
            return BadRequest(new { error = "Prompt is required." });

        var prompt = request.Prompt.Trim();
        var detection = detectionService.Analyze(prompt);
        var decision = await policyService.DecideAsync(prompt, detection, cancellationToken);
        var matchedRules = detection.RuleMatches
            .Select(x => new PolicyRuleMatchDto(x.RuleId, x.Name, x.Severity, "Detected", x.Explanation))
            .Concat(decision.MatchedPolicies.Select(x => new PolicyRuleMatchDto(
                $"policy.{x.Id}",
                x.Name,
                SeverityFromRisk(x.RiskScore),
                ToDisplayDecision(x.Action),
                x.Explanation)))
            .DefaultIfEmpty(BuildPolicyRule(decision.Action, decision.Reason))
            .ToList();

        return Ok(new PolicySimulationResponse(
            ToDisplayDecision(decision.Action),
            decision.RiskScore,
            decision.Reason,
            detection.RedactedText,
            decision.Action == "mask" ? decision.PromptToSend : null,
            matchedRules,
            detection.Findings.Select(x => new SensitiveFindingDto(x.Type, x.Severity, x.Description)).ToList()));
    }

    private static PolicyRuleMatchDto BuildPolicyRule(string action, string? reason)
    {
        return action switch
        {
            "block" => new("policy.block_secrets", "Block secrets", "Critical", "Block", reason ?? "Secret material was found, so the prompt is blocked before model use."),
            "mask" => new("policy.mask_sensitive_data", "Mask sensitive data", "Medium", "Mask", reason ?? "Sensitive data was found, so the prompt is masked before model use."),
            _ => new("policy.allow", "Allow safe prompt", "Low", "Allow", "No blocking or masking rule matched, so the prompt is allowed.")
        };
    }

    private static string SeverityFromRisk(int riskScore)
    {
        return riskScore >= 80 ? "Critical" : riskScore >= 60 ? "High" : riskScore >= 30 ? "Medium" : "Low";
    }

    private static string ToDisplayDecision(string action)
    {
        return action switch
        {
            "block" => "Block",
            "mask" => "Mask",
            _ => "Allow"
        };
    }
}
