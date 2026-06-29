export interface ChatRequest {
  message: string;
  model?: string;
  provider?: string;
  sessionId?: string;
  applicationId?: string;
  appKey?: string;
}

export interface SensitiveFinding {
  type: string;
  severity: string;
  description: string;
}

export interface PolicyRuleMatch {
  ruleId: string;
  name: string;
  severity: string;
  outcome: string;
  explanation: string;
}

export interface PolicySimulationResponse {
  decision: 'Allow' | 'Mask' | 'Block';
  riskScore: number;
  reason?: string;
  promptPreview: string;
  maskedPreview?: string;
  matchedRules: PolicyRuleMatch[];
  findings: SensitiveFinding[];
}

export interface ChatResponse {
  requestId: string;
  sessionId: string;
  applicationId?: string;
  applicationKey?: string;
  applicationName?: string;
  applicationOwner?: string;
  applicationEnvironment?: string;
  provider: string;
  model: string;
  status: string;
  policyAction: string;
  policyReason?: string;
  response: string;
  riskScore: number;
  totalDurationMs?: number;
  policyCheckDurationMs?: number;
  ollamaDurationMs?: number;
  databaseDurationMs?: number;
  promptLength: number;
  responseLength: number;
  promptTokens?: number;
  responseTokens?: number;
  totalTokens?: number;
  llmTokensUsed: number;
  tokensSaved: number;
  cachedTokensServed: number;
  cacheHit: boolean;
  exactCacheHit: boolean;
  semanticCacheHit: boolean;
  similarityScore?: number;
  wasBlockedBeforeModelCall: boolean;
  errorMessage?: string;
  findings: SensitiveFinding[];
}

export interface ChatHistoryItem {
  id: string;
  timestampUtc: string;
  applicationId?: string;
  applicationKey?: string;
  applicationName?: string;
  applicationOwner?: string;
  applicationEnvironment?: string;
  provider: string;
  model: string;
  status: string;
  policyAction: string;
  riskScore: number;
  promptPreview: string;
  responsePreview?: string;
  totalDurationMs?: number;
  ollamaDurationMs?: number;
  promptLength: number;
  responseLength: number;
  promptTokens?: number;
  responseTokens?: number;
  totalTokens?: number;
  llmTokensUsed: number;
  tokensSaved: number;
  cachedTokensServed: number;
  cacheHit: boolean;
  exactCacheHit: boolean;
  semanticCacheHit: boolean;
  similarityScore?: number;
  errorMessage?: string;
  matchedPolicyName?: string;
  findingCount: number;
}

export interface SecurityPolicy {
  id: string;
  name: string;
  description: string;
  findingType: string;
  action: "Allow" | "Mask" | "Block";
  riskScore: number;
  enabled: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface SecurityPolicyRequest {
  name: string;
  description?: string;
  findingType: string;
  action: "Allow" | "Mask" | "Block";
  riskScore: number;
  enabled: boolean;
}

export interface AiApplication {
  id: string;
  appKey: string;
  name: string;
  owner: string;
  environment: "Dev" | "Test" | "Prod";
  description: string;
  enabled: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface AiApplicationRequest {
  name: string;
  owner: string;
  environment: "Dev" | "Test" | "Prod";
  description?: string;
  appKey?: string;
  enabled: boolean;
}

export interface Dashboard {
  totalRequests: number;
  successCount: number;
  blockedCount: number;
  policyViolations: number;
  sensitiveDataFindings: number;
  averageResponseTimeMs: number;
  fastestRequestMs?: number;
  slowestRequestMs?: number;
  lastRequestTime?: string;
  totalTokensUsed: number;
  totalTokensSaved: number;
  totalCachedTokensServed: number;
  averageTokensPerRequest: number;
  cacheHitCount: number;
  exactCacheHitCount: number;
  semanticCacheHitCount: number;
  cacheMissCount: number;
  blockedSecretsCount: number;
  providerUsage: { provider: string; count: number }[];
  averageDurationByProvider: { provider: string; averageDurationMs: number }[];
  tokensByProvider: { provider: string; llmTokensUsed: number; tokensSaved: number }[];
  applicationUsage: { applicationName: string; environment: string; count: number }[];
  violationsByApplication: { applicationName: string; environment: string; count: number }[];
  tokensByApplication: { applicationName: string; environment: string; llmTokensUsed: number; tokensSaved: number }[];
  averageDurationByApplication: { applicationName: string; environment: string; averageDurationMs: number }[];
  modelUsage: { model: string; count: number }[];
  policyActions: { action: string; count: number }[];
  findingTypes: { type: string; count: number }[];
  recentHighRiskRequests: ChatHistoryItem[];
  recentRequests: ChatHistoryItem[];
}

export interface ChatSession {
  id: string;
  title: string;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface StoredChatMessage {
  id: string;
  sessionId: string;
  role: 'user' | 'assistant' | 'system';
  content: string;
  createdAtUtc: string;
  model?: string;
  policyDecision?: string;
  riskScore?: number;
  requestId?: string;
}
