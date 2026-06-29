namespace AiAgentSecurityScanner.Api.Models;

public class ChatRequestLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public DateTime RequestStartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? RequestCompletedAtUtc { get; set; }
    public long? TotalDurationMs { get; set; }
    public long? PolicyCheckDurationMs { get; set; }
    public long? OllamaDurationMs { get; set; }
    public long? DatabaseDurationMs { get; set; }
    public int? PromptTokens { get; set; }
    public int? ResponseTokens { get; set; }
    public int? TotalTokens { get; set; }
    public int LlmTokensUsed { get; set; }
    public int TokensSaved { get; set; }
    public int CachedTokensServed { get; set; }
    public bool CacheHit { get; set; }
    public bool ExactCacheHit { get; set; }
    public bool SemanticCacheHit { get; set; }
    public double? SimilarityScore { get; set; }
    public string? CacheKey { get; set; }
    public bool WasBlockedBeforeModelCall { get; set; }
    public Guid? ApplicationId { get; set; }
    public string? ApplicationKey { get; set; }
    public string? ApplicationName { get; set; }
    public string? ApplicationOwner { get; set; }
    public string? ApplicationEnvironment { get; set; }
    public required string Model { get; set; }
    public string Provider { get; set; } = "Ollama";
    public required string PromptPreview { get; set; }
    public string? ResponsePreview { get; set; }
    public int PromptLength { get; set; }
    public int ResponseLength { get; set; }
    public required string Status { get; set; }
    public required string PolicyAction { get; set; }
    public string? PolicyReason { get; set; }
    public string? ErrorMessage { get; set; }
    public int RiskScore { get; set; }
    public string? MatchedPolicyName { get; set; }
    public string? SourceIp { get; set; }
    public List<SensitiveDataFinding> Findings { get; set; } = [];
}

public class ChatSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "New chat";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<ChatMessage> Messages { get; set; } = [];
}

public class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public ChatSession Session { get; set; } = null!;
    public required string Role { get; set; }
    public required string Content { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? Model { get; set; }
    public string? PolicyDecision { get; set; }
    public int? RiskScore { get; set; }
    public Guid? RequestId { get; set; }
}

public class SensitiveDataFinding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChatRequestLogId { get; set; }
    public ChatRequestLog ChatRequestLog { get; set; } = null!;
    public required string Type { get; set; }
    public required string Severity { get; set; }
    public required string Description { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class SemanticCacheEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string OriginalPrompt { get; set; }
    public required string NormalizedPrompt { get; set; }
    public required string EmbeddingJson { get; set; }
    public required string CachedResponse { get; set; }
    public required string Model { get; set; }
    public required string PolicyDecision { get; set; }
    public int? PromptTokens { get; set; }
    public int? ResponseTokens { get; set; }
    public int? TotalTokens { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class SecurityPolicy
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public string Description { get; set; } = string.Empty;
    public required string FindingType { get; set; }
    public required string Action { get; set; }
    public int RiskScore { get; set; }
    public bool Enabled { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class AiApplication
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string AppKey { get; set; }
    public required string Name { get; set; }
    public required string Owner { get; set; }
    public required string Environment { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
