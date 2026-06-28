namespace AiAgentSecurityScanner.Api.DTOs;

public record ChatRequest(string Message, string? Model, Guid? SessionId);

public record ChatResponse(
    Guid RequestId,
    Guid SessionId,
    string Model,
    string Status,
    string PolicyAction,
    string? PolicyReason,
    string Response,
    int RiskScore,
    long? TotalDurationMs,
    long? PolicyCheckDurationMs,
    long? OllamaDurationMs,
    long? DatabaseDurationMs,
    int PromptLength,
    int ResponseLength,
    int? PromptTokens,
    int? ResponseTokens,
    int? TotalTokens,
    int LlmTokensUsed,
    int TokensSaved,
    int CachedTokensServed,
    bool CacheHit,
    bool ExactCacheHit,
    bool SemanticCacheHit,
    double? SimilarityScore,
    bool WasBlockedBeforeModelCall,
    string? ErrorMessage,
    IReadOnlyList<SensitiveFindingDto> Findings);

public record SensitiveFindingDto(
    string Type,
    string Severity,
    string Description);

public record ChatHistoryItemDto(
    Guid Id,
    DateTime TimestampUtc,
    string Model,
    string Status,
    string PolicyAction,
    int RiskScore,
    string PromptPreview,
    string? ResponsePreview,
    long? TotalDurationMs,
    long? OllamaDurationMs,
    int PromptLength,
    int ResponseLength,
    int? PromptTokens,
    int? ResponseTokens,
    int? TotalTokens,
    int LlmTokensUsed,
    int TokensSaved,
    int CachedTokensServed,
    bool CacheHit,
    bool ExactCacheHit,
    bool SemanticCacheHit,
    double? SimilarityScore,
    string? ErrorMessage,
    int FindingCount);

public record DashboardDto(
    int TotalRequests,
    int SuccessCount,
    int BlockedCount,
    int PolicyViolations,
    int SensitiveDataFindings,
    double AverageResponseTimeMs,
    long? FastestRequestMs,
    long? SlowestRequestMs,
    DateTime? LastRequestTime,
    long TotalTokensUsed,
    long TotalTokensSaved,
    long TotalCachedTokensServed,
    double AverageTokensPerRequest,
    int CacheHitCount,
    int ExactCacheHitCount,
    int SemanticCacheHitCount,
    int CacheMissCount,
    IReadOnlyList<ModelUsageDto> ModelUsage,
    IReadOnlyList<PolicyActionCountDto> PolicyActions,
    IReadOnlyList<SensitiveFindingCountDto> FindingTypes,
    IReadOnlyList<ChatHistoryItemDto> RecentRequests);

public record ModelUsageDto(string Model, int Count);
public record PolicyActionCountDto(string Action, int Count);
public record SensitiveFindingCountDto(string Type, int Count);

public record ChatSessionDto(Guid Id, string Title, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);

public record ChatMessageDto(
    Guid Id,
    Guid SessionId,
    string Role,
    string Content,
    DateTime CreatedAtUtc,
    string? Model,
    string? PolicyDecision,
    int? RiskScore,
    Guid? RequestId);

public record CreateChatSessionRequest(string? Title);
