# AI Security Gateway MVP

Simple local MVP for testing AI security controls in front of a local model.

The app has:

- Angular Chat UI
- .NET 9 Gateway API
- Ollama local model integration
- SQLite request logging
- Regex-based sensitive data detection
- Simple policy engine: `allow`, `mask`, or `block`
- Dashboard based only on real logged requests

No mock dashboard data is seeded.

## Architecture

```text
Angular Chat UI
→ .NET Gateway API
→ Sensitive data detection
→ Policy engine
→ Ollama local API
→ SQLite logs
→ Angular Dashboard
```

## What the gateway does

When the chat UI sends a prompt to `POST /api/chat`, the gateway:

1. Scans the prompt for sensitive data.
2. Applies a simple policy decision:
   - `allow`: no sensitive data found.
   - `mask`: PII found, so the prompt is redacted before Ollama receives it.
   - `block`: secret found, so the request is not sent to Ollama.
3. Calls local Ollama if the request is allowed or masked.
4. Logs the request, response preview, model, status, policy action, risk score, and findings to SQLite.
5. Returns the model response or block message to the chat UI.

## Sensitive data detection

The MVP detects:

- Email addresses
- Phone numbers
- Social Security numbers
- `password=...`
- `api_key=...` / `api-key=...`
- Bearer tokens
- AWS access key IDs
- Basic database connection string patterns

## Models

Preferred model:

```text
llama3.2:1b
```

Fallback model:

```text
qwen2.5:0.5b
```

## Prerequisites

- .NET 9 SDK
- Node.js 20.14.0 or compatible with the Angular version in this repo
- Ollama

Install and start Ollama from [https://ollama.com](https://ollama.com), then pull at least one small model:

```bash
ollama pull llama3.2:1b
```

If storage or memory is tight:

```bash
ollama pull qwen2.5:0.5b
```

For semantic cache, pull the local embedding model:

```bash
ollama pull nomic-embed-text
```

Ollama should be reachable at:

```text
http://localhost:11434/api/chat
```

## Run the backend

```bash
cd backend/AiAgentSecurityScanner.Api
dotnet restore
dotnet run --launch-profile http
```

The API runs at:

```text
http://localhost:5187
```

Swagger:

```text
http://localhost:5187/swagger
```

SQLite database file:

```text
backend/AiAgentSecurityScanner.Api/ai-security-gateway.db
```

## Run the Angular app

In a second terminal:

```bash
cd frontend
npm install
npm start
```

Open:

```text
http://localhost:4300
```

The Security Gateway UI runs on port `4300`. RagApp uses port `4200`.

## Test from curl

Safe prompt:

```bash
curl -X POST http://localhost:5187/api/chat \
  -H "Content-Type: application/json" \
  -d '{
    "appKey": "security-gateway-chat",
    "model": "llama3.2:1b",
    "message": "Explain how to reset a laptop password in simple steps."
  }'
```

Risky prompt with PII and a secret:

```bash
curl -X POST http://localhost:5187/api/chat \
  -H "Content-Type: application/json" \
  -d '{
    "appKey": "security-gateway-chat",
    "model": "llama3.2:1b",
    "message": "Send email to hr@company.com about employee SSN 123-45-6789 and password=Secret123"
  }'
```

Expected result:

- Request is logged in SQLite.
- Sensitive findings are saved.
- Secret prompt is blocked.
- Dashboard counts update from real data.

Dashboard API:

```bash
curl http://localhost:5187/api/dashboard
```

Chat history API:

```bash
curl http://localhost:5187/api/chat/history
```

## API endpoints

| Endpoint | Purpose |
|---|---|
| `POST /api/chat` | Main chat gateway endpoint. Use `appKey` to identify the caller, such as `security-gateway-chat` or `rag-app`. |
| `GET /api/chat/history` | Shows recent real logged gateway requests across applications. |
| `GET /api/chat/sessions` | Shows only built-in Security Gateway Chat UI sessions. |
| `DELETE /api/chat/sessions` | Clears only built-in Security Gateway Chat UI sessions; audit request logs are preserved. |
| `DELETE /api/chat/sessions/legacy` | Clears legacy/external chat-session rows from the chat UI store; audit request logs are preserved. |
| `GET /api/dashboard` | Returns real dashboard counts from SQLite. |
| `GET /api/health` | Health check. |

## Configuration

Backend config lives in:

- `backend/AiAgentSecurityScanner.Api/appsettings.json`
- `backend/AiAgentSecurityScanner.Api/appsettings.Development.json`

Relevant settings:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=ai-security-gateway.db"
  },
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "DefaultModel": "llama3.2:1b",
    "FallbackModel": "qwen2.5:0.5b",
    "TimeoutSeconds": 60,
    "KeepAlive": "10m",
    "NumPredict": 120
  },
  "AiSecurityPolicy": {
    "PromptPreviewMaxLength": 1000,
    "ResponsePreviewMaxLength": 1000,
    "BlockOnSecrets": true,
    "MaskPii": true
  },
  "SemanticCache": {
    "Enabled": true,
    "EmbeddingModel": "nomic-embed-text",
    "SimilarityThreshold": 0.95
  }
}
```

## Notes

- PostgreSQL is no longer required.
- Docker Compose is not required for the current SQLite MVP.
- The previous config scanner, MCP inventory, vector DB graph, and sample-agent modules were removed from this simplified MVP scope.
