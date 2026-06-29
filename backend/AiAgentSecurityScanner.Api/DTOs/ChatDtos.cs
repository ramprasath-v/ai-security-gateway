namespace AiAgentSecurityScanner.Api.DTOs;

public record ChatRequest(string Message, string? Model, string? Provider, Guid? SessionId, Guid? ApplicationId, string? AppKey);

public record ChatResponse(
    Guid RequestId,
    Guid SessionId,
    Guid? ApplicationId,
    string? ApplicationKey,
    string? ApplicationName,
    string? ApplicationOwner,
    string? ApplicationEnvironment,
    string Provider,
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

public record PolicySimulationRequest(string Prompt);

public record PolicyRuleMatchDto(
    string RuleId,
    string Name,
    string Severity,
    string Outcome,
    string Explanation);

public record PolicySimulationResponse(
    string Decision,
    int RiskScore,
    string? Reason,
    string PromptPreview,
    string? MaskedPreview,
    IReadOnlyList<PolicyRuleMatchDto> MatchedRules,
    IReadOnlyList<SensitiveFindingDto> Findings);

public record SecurityPolicyDto(
    Guid Id,
    string Name,
    string Description,
    string FindingType,
    string Action,
    int RiskScore,
    bool Enabled,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public record SecurityPolicyRequest(
    string Name,
    string? Description,
    string FindingType,
    string Action,
    int RiskScore,
    bool Enabled);

public record AiApplicationDto(
    Guid Id,
    string AppKey,
    string Name,
    string Owner,
    string Environment,
    string Description,
    bool Enabled,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public record AiApplicationRequest(
    string Name,
    string Owner,
    string Environment,
    string? Description,
    string? AppKey,
    bool Enabled);

public record ChatHistoryItemDto(
    Guid Id,
    DateTime TimestampUtc,
    Guid? ApplicationId,
    string? ApplicationKey,
    string? ApplicationName,
    string? ApplicationOwner,
    string? ApplicationEnvironment,
    string Provider,
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
    string? MatchedPolicyName,
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
    int BlockedSecretsCount,
    IReadOnlyList<ProviderUsageDto> ProviderUsage,
    IReadOnlyList<ProviderDurationDto> AverageDurationByProvider,
    IReadOnlyList<ProviderTokensDto> TokensByProvider,
    IReadOnlyList<ApplicationUsageDto> ApplicationUsage,
    IReadOnlyList<ApplicationViolationDto> ViolationsByApplication,
    IReadOnlyList<ApplicationTokensDto> TokensByApplication,
    IReadOnlyList<ApplicationDurationDto> AverageDurationByApplication,
    IReadOnlyList<ModelUsageDto> ModelUsage,
    IReadOnlyList<PolicyActionCountDto> PolicyActions,
    IReadOnlyList<SensitiveFindingCountDto> FindingTypes,
    IReadOnlyList<ChatHistoryItemDto> RecentHighRiskRequests,
    IReadOnlyList<ChatHistoryItemDto> RecentRequests);

public record ModelUsageDto(string Model, int Count);
public record ProviderUsageDto(string Provider, int Count);
public record ProviderDurationDto(string Provider, double AverageDurationMs);
public record ProviderTokensDto(string Provider, long LlmTokensUsed, long TokensSaved);
public record ApplicationUsageDto(string ApplicationName, string Environment, int Count);
public record ApplicationViolationDto(string ApplicationName, string Environment, int Count);
public record ApplicationTokensDto(string ApplicationName, string Environment, long LlmTokensUsed, long TokensSaved);
public record ApplicationDurationDto(string ApplicationName, string Environment, double AverageDurationMs);
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
