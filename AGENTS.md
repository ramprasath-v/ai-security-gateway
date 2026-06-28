# AI Security Gateway MVP Rules

This project must stay simple.

## Current MVP

- Angular Chat UI
- .NET 9 Gateway API
- Ollama local model integration
- SQLite database
- Sensitive data detection
- Simple policy engine
- Dashboard

## Ollama

Use local Ollama API:
http://localhost:11434/api/chat

Default model:
llama3.2:1b

Fallback model:
qwen2.5:0.5b

## Not Allowed Yet

- Auto discovery
- MCP scanning
- GitHub scanning
- Kubernetes scanning
- Cloud scanning
- Complex RBAC
- Multi-tenant SaaS

## Rules

- Do not add features outside MVP.
- Do not create mock dashboard data.
- Do not add new libraries unless required.
- Keep project buildable.
- Ask before deleting large parts of code.
