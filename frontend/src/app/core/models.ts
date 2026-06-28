export interface ChatRequest {
  message: string;
  model?: string;
  sessionId?: string;
}

export interface SensitiveFinding {
  type: string;
  severity: string;
  description: string;
}

export interface ChatResponse {
  requestId: string;
  sessionId: string;
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
  cacheHit: boolean;
  exactCacheHit: boolean;
  semanticCacheHit: boolean;
  similarityScore?: number;
  errorMessage?: string;
  findingCount: number;
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
  averageTokensPerRequest: number;
  cacheHitCount: number;
  exactCacheHitCount: number;
  semanticCacheHitCount: number;
  cacheMissCount: number;
  modelUsage: { model: string; count: number }[];
  policyActions: { action: string; count: number }[];
  findingTypes: { type: string; count: number }[];
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
