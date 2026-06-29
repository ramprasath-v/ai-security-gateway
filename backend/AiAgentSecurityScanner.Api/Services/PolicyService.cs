namespace AiAgentSecurityScanner.Api.Services;

using AiAgentSecurityScanner.Api.Data;
using AiAgentSecurityScanner.Api.Models;
using Microsoft.EntityFrameworkCore;

public record MatchedPolicy(
    Guid Id,
    string Name,
    string Description,
    string FindingType,
    string Action,
    int RiskScore,
    string Explanation);

public record PolicyDecision(
    string Action,
    string? Reason,
    string PromptToSend,
    int RiskScore,
    IReadOnlyList<MatchedPolicy> MatchedPolicies);

public interface IPolicyService
{
    Task<PolicyDecision> DecideAsync(string prompt, AiSecurityDetectionResult detection, CancellationToken cancellationToken);
}

public class PolicyService(AppDbContext db) : IPolicyService
{
    public async Task<PolicyDecision> DecideAsync(string prompt, AiSecurityDetectionResult detection, CancellationToken cancellationToken)
    {
        var enabledPolicies = await db.SecurityPolicies
            .AsNoTracking()
            .Where(x => x.Enabled)
            .ToListAsync(cancellationToken);

        enabledPolicies = enabledPolicies
            .OrderByDescending(x => ActionRank(x.Action))
            .ThenByDescending(x => x.RiskScore)
            .ToList();

        var matchedPolicies = enabledPolicies
            .Where(policy => detection.Findings.Any(finding => IsPolicyMatch(policy.FindingType, finding.Type)))
            .Select(policy => new MatchedPolicy(
                policy.Id,
                policy.Name,
                policy.Description,
                policy.FindingType,
                NormalizeAction(policy.Action),
                policy.RiskScore,
                BuildExplanation(policy)))
            .ToList();

        if (matchedPolicies.Count > 0)
        {
            var highestAction = matchedPolicies
                .OrderByDescending(x => ActionRank(x.Action))
                .ThenByDescending(x => x.RiskScore)
                .First();
            var riskScore = Math.Min(100, Math.Max(detection.RiskScore, matchedPolicies.Max(x => x.RiskScore)));
            var reason = $"{highestAction.Name} matched {highestAction.FindingType}.";

            return NormalizeAction(highestAction.Action) switch
            {
                "block" => new PolicyDecision("block", reason, detection.RedactedText, riskScore, matchedPolicies),
                "mask" => new PolicyDecision("mask", reason, detection.RedactedText, riskScore, matchedPolicies),
                _ => new PolicyDecision("allow", reason, prompt, riskScore, matchedPolicies)
            };
        }

        return new PolicyDecision("allow", null, prompt, detection.RiskScore, []);
    }

    private static bool IsPolicyMatch(string policyFindingType, string detectedFindingType)
    {
        var policyType = NormalizeFindingType(policyFindingType);
        var detectedType = NormalizeFindingType(detectedFindingType);

        if (policyType == detectedType) return true;

        return policyType == "api key" && detectedType is
            "secret" or
            "openai api key" or
            "aws access key" or
            "github token" or
            "database connection string";
    }

    private static string NormalizeFindingType(string value)
    {
        return value.Trim().ToLowerInvariant().Replace("-", " ");
    }

    private static string NormalizeAction(string action)
    {
        return action.Trim().ToLowerInvariant() switch
        {
            "block" => "block",
            "mask" => "mask",
            _ => "allow"
        };
    }

    private static int ActionRank(string action)
    {
        return NormalizeAction(action) switch
        {
            "block" => 3,
            "mask" => 2,
            _ => 1
        };
    }

    private static string BuildExplanation(SecurityPolicy policy)
    {
        var action = NormalizeAction(policy.Action) switch
        {
            "block" => "blocks the prompt before model use",
            "mask" => "masks the prompt before model use",
            _ => "allows the prompt"
        };

        return $"{policy.Name} matches {policy.FindingType} findings and {action}.";
    }
}
