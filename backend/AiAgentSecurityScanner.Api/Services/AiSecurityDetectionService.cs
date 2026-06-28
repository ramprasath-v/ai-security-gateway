using System.Text.RegularExpressions;

namespace AiAgentSecurityScanner.Api.Services;

public record AiSecurityFinding(string Type, string Severity, string Description);

public record AiSecurityDetectionResult(
    bool HasSensitiveData,
    bool HasSecrets,
    int RiskScore,
    string RedactedText,
    IReadOnlyList<AiSecurityFinding> Findings);

public interface IAiSecurityDetectionService
{
    AiSecurityDetectionResult Analyze(string text);
}

public partial class AiSecurityDetectionService : IAiSecurityDetectionService
{
    public AiSecurityDetectionResult Analyze(string text)
    {
        text ??= string.Empty;
        var findings = new List<AiSecurityFinding>();
        var riskScore = 0;

        if (EmailPattern().IsMatch(text))
        {
            findings.Add(new("Email", "Medium", "Email address detected in the prompt."));
            riskScore += 20;
        }

        if (PhonePattern().IsMatch(text))
        {
            findings.Add(new("Phone", "Medium", "Phone number detected in the prompt."));
            riskScore += 20;
        }

        if (SsnPattern().IsMatch(text))
        {
            findings.Add(new("SSN", "High", "Social Security number detected in the prompt."));
            riskScore += 40;
        }

        if (SecretPattern().IsMatch(text))
        {
            findings.Add(new("Secret", "Critical", "Credential, API key, password, bearer token, or connection string detected."));
            riskScore += 60;
        }

        riskScore = Math.Min(100, riskScore);
        var hasSecrets = findings.Any(x => x.Type == "Secret");
        var hasSensitiveData = findings.Any(x => x.Type is "Email" or "Phone" or "SSN");

        return new AiSecurityDetectionResult(
            hasSensitiveData,
            hasSecrets,
            riskScore,
            Redact(text),
            findings);
    }

    private static string Redact(string text)
    {
        var redacted = EmailPattern().Replace(text, "[REDACTED_EMAIL]");
        redacted = PhonePattern().Replace(redacted, "[REDACTED_PHONE]");
        redacted = SsnPattern().Replace(redacted, "[REDACTED_SSN]");
        redacted = SecretPattern().Replace(redacted, match =>
            match.Value.StartsWith("Bearer", StringComparison.OrdinalIgnoreCase) ? "Bearer [REDACTED]" : "[REDACTED_SECRET]");
        return redacted;
    }

    [GeneratedRegex(@"(?i)\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"(?<!\w)(?:\+?1[-.\s]?)?(?:\(?[2-9]\d{2}\)?[-.\s]?)\d{3}[-.\s]?\d{4}(?!\w)")]
    private static partial Regex PhonePattern();

    [GeneratedRegex(@"(?<!\d)(?!000|666|9\d{2})\d{3}[- ]?(?!00)\d{2}[- ]?(?!0000)\d{4}(?!\d)")]
    private static partial Regex SsnPattern();

    [GeneratedRegex(@"(?i)(\bpassword\s*=\s*[^\s,;]+|\bapi[_-]?key\s*=\s*[^\s,;]+|\bBearer\s+[A-Z0-9._~+\-/]+=*|\b(?:AKIA|ASIA)[0-9A-Z]{16}\b|\b(?:Server|Host|Data Source)\s*=\s*[^;\r\n]+;\s*(?:Database|Initial Catalog)\s*=\s*[^;\r\n]+(?:;[^\r\n]*)?)")]
    private static partial Regex SecretPattern();
}
