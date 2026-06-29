using System.Text.RegularExpressions;

namespace AiAgentSecurityScanner.Api.Services;

public record AiSecurityFinding(string Type, string Severity, string Description);

public record AiSecurityRuleMatch(
    string RuleId,
    string Name,
    string Severity,
    string Explanation);

public record AiSecurityDetectionResult(
    bool HasSensitiveData,
    bool HasSecrets,
    int RiskScore,
    string RedactedText,
    IReadOnlyList<AiSecurityFinding> Findings,
    IReadOnlyList<AiSecurityRuleMatch> RuleMatches);

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
        var rules = new List<AiSecurityRuleMatch>();
        var riskScore = 0;

        if (EmailPattern().IsMatch(text))
        {
            AddFinding(findings, rules, "Email", "Medium", "Email address detected in the prompt.", "pii.email", "Email address", "Email addresses can identify a person or account and should be masked before model use.");
            riskScore += 15;
        }

        if (PhonePattern().IsMatch(text))
        {
            AddFinding(findings, rules, "Phone", "Medium", "Phone number detected in the prompt.", "pii.phone", "Phone number", "Phone numbers are personal data and should be masked before model use.");
            riskScore += 20;
        }

        if (SsnPattern().IsMatch(text))
        {
            AddFinding(findings, rules, "SSN", "High", "Social Security number detected in the prompt.", "pii.ssn", "Social Security number", "SSNs are high-risk personal identifiers and should not be sent to the model in clear text.");
            riskScore += 40;
        }

        if (CreditCardPattern().Matches(text).Any(match => IsLikelyCreditCard(match.Value)))
        {
            AddFinding(findings, rules, "CreditCard", "High", "Credit card number detected in the prompt.", "pii.credit_card", "Credit card number", "Payment card numbers are high-risk sensitive data and should be masked.");
            riskScore += 45;
        }

        if (OpenAiApiKeyPattern().IsMatch(text))
        {
            AddFinding(findings, rules, "OpenAI API Key", "Critical", "OpenAI API key detected in the prompt.", "secret.openai_api_key", "OpenAI API key", "OpenAI API keys grant model/API access and must be blocked.");
            riskScore += 70;
        }

        if (AwsAccessKeyPattern().IsMatch(text))
        {
            AddFinding(findings, rules, "AWS Access Key", "Critical", "AWS access key detected in the prompt.", "secret.aws_access_key", "AWS access key", "AWS access keys can grant cloud account access and must be blocked.");
            riskScore += 70;
        }

        if (GitHubTokenPattern().IsMatch(text))
        {
            AddFinding(findings, rules, "GitHub Token", "Critical", "GitHub token detected in the prompt.", "secret.github_token", "GitHub token", "GitHub tokens can grant repository or organization access and must be blocked.");
            riskScore += 70;
        }

        if (JwtPattern().IsMatch(text))
        {
            AddFinding(findings, rules, "JWT", "Critical", "JWT token detected in the prompt.", "secret.jwt", "JWT token", "JWTs can carry authentication claims and must be blocked.");
            riskScore += 65;
        }

        if (DatabaseConnectionStringPattern().IsMatch(text))
        {
            AddFinding(findings, rules, "Database Connection String", "Critical", "Database connection string detected in the prompt.", "secret.database_connection_string", "Database connection string", "Database connection strings often include hostnames, usernames, or passwords and must be blocked.");
            riskScore += 70;
        }

        if (PrivateKeyBlockPattern().IsMatch(text))
        {
            AddFinding(findings, rules, "Private Key", "Critical", "Private key block detected in the prompt.", "secret.private_key", "Private key block", "Private keys can authenticate systems or users and must be blocked.");
            riskScore += 80;
        }

        if (GenericSecretPattern().IsMatch(text))
        {
            AddFinding(findings, rules, "Secret", "Critical", "Credential, API key, password, or bearer token detected.", "secret.generic_credential", "Generic credential", "Credentials and bearer tokens can grant account or service access and must be blocked.");
            riskScore += 60;
        }

        riskScore = Math.Min(100, riskScore);
        var hasSecrets = findings.Any(x => IsSecretType(x.Type));
        var hasSensitiveData = findings.Any(x => x.Type is "Email" or "Phone" or "SSN" or "CreditCard");

        return new AiSecurityDetectionResult(
            hasSensitiveData,
            hasSecrets,
            riskScore,
            Redact(text),
            findings,
            rules);
    }

    private static string Redact(string text)
    {
        var redacted = PrivateKeyBlockPattern().Replace(text, "[REDACTED_PRIVATE_KEY]");
        redacted = DatabaseConnectionStringPattern().Replace(redacted, "[REDACTED_DB_CONNECTION]");
        redacted = OpenAiApiKeyPattern().Replace(redacted, "[REDACTED_OPENAI_KEY]");
        redacted = AwsAccessKeyPattern().Replace(redacted, "[REDACTED_AWS_KEY]");
        redacted = GitHubTokenPattern().Replace(redacted, "[REDACTED_GITHUB_TOKEN]");
        redacted = JwtPattern().Replace(redacted, "[REDACTED_JWT]");
        redacted = GenericSecretPattern().Replace(redacted, match =>
            match.Value.StartsWith("Bearer", StringComparison.OrdinalIgnoreCase) ? "Bearer [REDACTED]" : "[REDACTED_SECRET]");
        redacted = EmailPattern().Replace(redacted, "[REDACTED_EMAIL]");
        redacted = PhonePattern().Replace(redacted, "[REDACTED_PHONE]");
        redacted = SsnPattern().Replace(redacted, "[REDACTED_SSN]");
        redacted = CreditCardPattern().Replace(redacted, match => IsLikelyCreditCard(match.Value) ? "[REDACTED_CREDIT_CARD]" : match.Value);
        return redacted;
    }

    private static void AddFinding(
        List<AiSecurityFinding> findings,
        List<AiSecurityRuleMatch> rules,
        string type,
        string severity,
        string description,
        string ruleId,
        string ruleName,
        string explanation)
    {
        findings.Add(new(type, severity, description));
        rules.Add(new(ruleId, ruleName, severity, explanation));
    }

    private static bool IsSecretType(string type)
    {
        return type is "Secret" or "OpenAI API Key" or "AWS Access Key" or "GitHub Token" or "JWT" or "Database Connection String" or "Private Key";
    }

    private static bool IsLikelyCreditCard(string value)
    {
        var digits = value.Where(char.IsDigit).Select(x => x - '0').ToArray();
        if (digits.Length is < 13 or > 19) return false;

        var sum = 0;
        var alternate = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var digit = digits[i];
            if (alternate)
            {
                digit *= 2;
                if (digit > 9) digit -= 9;
            }
            sum += digit;
            alternate = !alternate;
        }

        return sum % 10 == 0;
    }

    [GeneratedRegex(@"(?i)\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"(?<!\w)(?:\+?1[-.\s]?)?(?:\(?[2-9]\d{2}\)?[-.\s]?)\d{3}[-.\s]?\d{4}(?!\w)")]
    private static partial Regex PhonePattern();

    [GeneratedRegex(@"(?<!\d)(?!000|666|9\d{2})\d{3}[- ]?(?!00)\d{2}[- ]?(?!0000)\d{4}(?!\d)")]
    private static partial Regex SsnPattern();

    [GeneratedRegex(@"(?<!\d)(?:\d[ -]?){13,19}(?!\d)")]
    private static partial Regex CreditCardPattern();

    [GeneratedRegex(@"\bsk-(?:proj-)?[A-Za-z0-9_-]{20,}\b")]
    private static partial Regex OpenAiApiKeyPattern();

    [GeneratedRegex(@"\b(?:AKIA|ASIA)[0-9A-Z]{16}\b")]
    private static partial Regex AwsAccessKeyPattern();

    [GeneratedRegex(@"\b(?:ghp|gho|ghu|ghs|ghr)_[A-Za-z0-9_]{20,}\b|\bgithub_pat_[A-Za-z0-9_]{20,}\b")]
    private static partial Regex GitHubTokenPattern();

    [GeneratedRegex(@"\beyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\b")]
    private static partial Regex JwtPattern();

    [GeneratedRegex(@"(?i)\b(?:Server|Host|Data Source|User ID|Uid|Password|Pwd|Database|Initial Catalog)\s*=\s*[^;\r\n]+(?:;\s*(?:Server|Host|Data Source|User ID|Uid|Password|Pwd|Database|Initial Catalog)\s*=\s*[^;\r\n]+)+")]
    private static partial Regex DatabaseConnectionStringPattern();

    [GeneratedRegex(@"-----BEGIN (?:RSA |EC |OPENSSH |DSA )?PRIVATE KEY-----[\s\S]+?-----END (?:RSA |EC |OPENSSH |DSA )?PRIVATE KEY-----")]
    private static partial Regex PrivateKeyBlockPattern();

    [GeneratedRegex(@"(?i)(\bpassword\s*=\s*[^\s,;]+|\bapi[_-]?key\s*=\s*[^\s,;]+|\bBearer\s+[A-Z0-9._~+\-/]+=*)")]
    private static partial Regex GenericSecretPattern();
}
