namespace AiAgentSecurityScanner.Api.Services;

using Microsoft.Extensions.Options;

public record PolicyDecision(string Action, string? Reason, string PromptToSend);

public interface IPolicyService
{
    PolicyDecision Decide(string prompt, AiSecurityDetectionResult detection);
}

public class PolicyService(IOptions<AiSecurityPolicyOptions> options) : IPolicyService
{
    public PolicyDecision Decide(string prompt, AiSecurityDetectionResult detection)
    {
        var policy = options.Value;

        if (detection.HasSecrets && policy.BlockOnSecrets)
            return new PolicyDecision("block", "Secret detected. The request was not sent to the model.", detection.RedactedText);

        if ((detection.HasSensitiveData || detection.HasSecrets) && policy.MaskPii)
            return new PolicyDecision("mask", "Sensitive data detected. The prompt was masked before it was sent to the model.", detection.RedactedText);

        return new PolicyDecision("allow", null, prompt);
    }
}
