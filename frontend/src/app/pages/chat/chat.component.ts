import { Component, ElementRef, ViewChild, inject } from "@angular/core";
import { DatePipe } from "@angular/common";
import { FormsModule } from "@angular/forms";
import { finalize } from "rxjs";
import { ApiService } from "../../core/api.service";
import { ChatResponse, ChatSession } from "../../core/models";

interface ChatMessage {
  id?: string;
  role: "user" | "assistant" | "system";
  content: string;
  createdAtUtc?: string;
  meta?: string;
  badges?: { label: string; tone: string }[];
  findings?: string[];
  loading?: boolean;
  error?: boolean;
}

@Component({
  standalone: true,
  imports: [FormsModule, DatePipe],
  template: ` <section class="page-head">
      <div>
        <!-- <span class="demo-kicker">Demo Mode</span> -->
        <h2>Secure AI Chat</h2>
        <!-- <p>
          Run the sample prompts to populate the dashboard with real SQLite
          request data.
        </p> -->
      </div>
      <button
        class="button secondary"
        type="button"
        (click)="newChat()"
        [disabled]="loading"
      >
        ＋ New chat
      </button>
    </section>

    <section class="chat-layout">
      <aside class="panel sessions-panel">
        <h3>Chats</h3>
        <div class="session-list">
          @for (session of sessions; track session.id) {
            <button
              type="button"
              [class.active]="session.id === activeSessionId"
              (click)="selectSession(session.id)"
              [disabled]="loading"
            >
              <b>{{ session.title }}</b>
              <span>{{ session.updatedAtUtc | date: "short" }}</span>
            </button>
          } @empty {
            <p>No chats yet.</p>
          }
        </div>
      </aside>

      <article class="panel chat-panel">
        <div class="chat-messages" #messagesEl>
          @for (message of messages; track $index) {
            <div [class]="'message ' + message.role">
              <div
                class="bubble"
                [class.loading]="message.loading"
                [class.error]="message.error"
              >
                @if (message.loading) {
                  <span class="typing"><i></i><i></i><i></i></span>
                }
                <p>{{ message.content }}</p>
                @if (message.badges?.length) {
                  <div class="badge-row">
                    @for (badge of message.badges; track badge.label) {
                      <span [class]="'badge ' + badge.tone">{{
                        badge.label
                      }}</span>
                    }
                  </div>
                }
                @if (message.createdAtUtc) {
                  <small>{{ message.createdAtUtc | date: "short" }}</small>
                }
                @if (message.meta) {
                  <small>{{ message.meta }}</small>
                }
                @if (message.findings?.length) {
                  <div class="finding-list">
                    @for (finding of message.findings; track finding) {
                      <span>{{ finding }}</span>
                    }
                  </div>
                }
              </div>
            </div>
          }
        </div>

        <form class="composer" (ngSubmit)="send()">
          <textarea
            name="prompt"
            [(ngModel)]="prompt"
            [disabled]="loading"
            placeholder="Ask the local model something..."
            rows="4"
          ></textarea>
          <div class="composer-actions">
            <select name="model" [(ngModel)]="model" [disabled]="loading">
              <option value="llama3.2:1b">llama3.2:1b</option>
              <option value="qwen2.5:0.5b">qwen2.5:0.5b (faster)</option>
            </select>
            <button
              class="button primary"
              [disabled]="loading || !prompt.trim()"
            >
              {{ loading ? "Thinking..." : "Send" }}
            </button>
          </div>
        </form>
      </article>

      <aside class="panel helper-panel">
        <h3>Try these</h3>
        <button
          class="prompt-chip safe"
          type="button"
          (click)="useSafe()"
          [disabled]="loading"
        >
          <b>Safe request</b><span>Allowed by policy</span>
        </button>
        <button
          class="prompt-chip danger"
          type="button"
          (click)="useRisky()"
          [disabled]="loading"
        >
          <b>Sensitive request</b><span>SSN + password blocked</span>
        </button>
        <button
          class="prompt-chip semantic"
          type="button"
          (click)="useSemanticSeed()"
          [disabled]="loading"
        >
          <b>Semantic cache seed</b><span>First request creates cache</span>
        </button>
        <button
          class="prompt-chip semantic"
          type="button"
          (click)="useSemanticFollowup()"
          [disabled]="loading"
        >
          <b>Semantic cache follow-up</b
          ><span>Similar prompt for cache hit</span>
        </button>
        <p>
          Dashboard cards update from real request logs: policy, tokens,
          response time, similarity score, and cache hit or miss.
        </p>
      </aside>
    </section>`,
})
export class ChatComponent {
  private api = inject(ApiService);
  @ViewChild("messagesEl") private messagesEl?: ElementRef<HTMLElement>;
  prompt = "";
  model = "llama3.2:1b";
  loading = false;
  sessions: ChatSession[] = [];
  activeSessionId?: string;
  messages: ChatMessage[] = [
    {
      role: "system",
      content:
        "Welcome. I scan prompts before forwarding allowed requests to local Ollama.",
    },
  ];

  constructor() {
    this.loadSessions();
  }

  newChat() {
    this.api.createSession().subscribe((session) => {
      this.sessions = [session, ...this.sessions];
      this.activeSessionId = session.id;
      this.messages = [
        {
          role: "system",
          content: "New chat started. Messages are stored in SQLite.",
        },
      ];
      this.scrollToBottom();
    });
  }

  selectSession(sessionId: string) {
    if (this.activeSessionId === sessionId) return;
    this.activeSessionId = sessionId;
    this.loadMessages(sessionId);
  }

  send() {
    const message = this.prompt.trim();
    if (!message) return;

    this.messages.push({ role: "user", content: message });
    this.prompt = "";
    this.loading = true;
    const thinkingId = crypto.randomUUID();
    const started = performance.now();
    this.messages.push({
      id: thinkingId,
      role: "assistant",
      content:
        "Thinking… scanning policy, checking semantic cache, then calling Ollama only if needed.",
      loading: true,
      meta: `Model: ${this.model}`,
      badges: [
        { label: "Policy check", tone: "info" },
        { label: "Semantic cache", tone: "semantic" },
      ],
    });
    this.scrollToBottom();

    this.api
      .sendChat({ message, model: this.model, sessionId: this.activeSessionId })
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: (response) =>
          this.replaceThinking(
            thinkingId,
            response,
            performance.now() - started,
          ),
        error: (error) =>
          this.replaceWithError(
            thinkingId,
            error?.error?.message ||
              "The gateway API could not be reached. Make sure the .NET backend and Ollama are running.",
            performance.now() - started,
          ),
      });
  }

  useSafe() {
    this.prompt =
      "Explain how to reset a laptop password in three short bullets.";
  }

  useRisky() {
    this.prompt =
      "Send email to hr@company.com about employee SSN 123-45-6789 and password=Secret123";
  }

  useSemanticSeed() {
    this.prompt = "Summarize password reset steps in 3 bullet points.";
  }

  useSemanticFollowup() {
    this.prompt = "Explain how to reset a forgotten password in three simple steps.";
  }

  private replaceThinking(
    id: string,
    response: ChatResponse,
    clientElapsedMs: number,
  ) {
    this.activeSessionId = response.sessionId;
    const index = this.messages.findIndex((message) => message.id === id);
    const message = {
      role: "assistant",
      content: response.response,
      meta: this.formatMeta(response, clientElapsedMs),
      badges: this.buildBadges(response),
      findings: response.findings.map((x) => `${x.severity}: ${x.type}`),
      error: response.status === "Failed",
    } satisfies ChatMessage;

    if (index >= 0) this.messages[index] = message;
    else this.messages.push(message);

    this.loadSessions(false);
    this.scrollToBottom();
  }

  private replaceWithError(id: string, error: string, clientElapsedMs: number) {
    const index = this.messages.findIndex((message) => message.id === id);
    const message: ChatMessage = {
      role: "assistant",
      content: error,
      meta: `Client waited ${Math.round(clientElapsedMs)} ms before the request failed.`,
      error: true,
    };

    if (index >= 0) this.messages[index] = message;
    else this.messages.push(message);
    this.scrollToBottom();
  }

  private buildBadges(response: ChatResponse) {
    const policyTone =
      response.policyAction === "block"
        ? "danger"
        : response.policyAction === "mask"
          ? "warning"
          : "success";
    const cacheLabel = response.exactCacheHit
      ? "Exact hit"
      : response.semanticCacheHit
        ? "Semantic hit"
        : response.cacheHit
          ? "Cache hit"
          : "Cache miss";
    const cacheTone = response.semanticCacheHit
      ? "semantic"
      : response.cacheHit
        ? "success"
        : "muted";
    const duration =
      response.totalDurationMs == null
        ? "Duration n/a"
        : `${response.totalDurationMs} ms`;
    return [
      { label: response.policyAction.toUpperCase(), tone: policyTone },
      { label: cacheLabel, tone: cacheTone },
      {
        label: duration,
        tone:
          response.totalDurationMs != null && response.totalDurationMs > 5000
            ? "warning"
            : "info",
      },
    ];
  }

  private formatMeta(response: ChatResponse, clientElapsedMs: number) {
    const gateway =
      response.totalDurationMs == null
        ? "n/a"
        : `${response.totalDurationMs} ms gateway`;
    const policy =
      response.policyCheckDurationMs == null
        ? "n/a"
        : `${response.policyCheckDurationMs} ms policy`;
    const ollama =
      response.ollamaDurationMs == null
        ? "no Ollama call"
        : `${response.ollamaDurationMs} ms Ollama`;
    const db =
      response.databaseDurationMs == null
        ? "n/a"
        : `${response.databaseDurationMs} ms DB`;
    const tokens = response.cacheHit
      ? `${response.llmTokensUsed} LLM tokens · ${response.tokensSaved} saved`
      : `${response.llmTokensUsed} LLM tokens`;
    const cache = response.exactCacheHit
      ? "exact cache hit"
      : response.semanticCacheHit
        ? `semantic cache hit ${response.similarityScore?.toFixed(3) ?? ""}`.trim()
        : response.cacheHit
          ? "cache hit"
          : "cache miss";
    return `${response.status} · ${cache} · policy: ${response.policyAction} · model: ${response.model} · risk: ${response.riskScore} · ${tokens} · ${gateway} · ${policy} · ${ollama} · ${db} · ${Math.round(clientElapsedMs)} ms browser`;
  }

  private loadSessions(selectFirst = true) {
    this.api.sessions().subscribe((sessions) => {
      this.sessions = sessions;
      if (selectFirst && !this.activeSessionId && sessions.length > 0) {
        this.activeSessionId = sessions[0].id;
        this.loadMessages(sessions[0].id);
      }
    });
  }

  private loadMessages(sessionId: string) {
    this.api.sessionMessages(sessionId).subscribe((messages) => {
      this.messages = messages.length
        ? messages.map((message) => ({
            id: message.id,
            role: message.role,
            content: message.content,
            createdAtUtc: message.createdAtUtc,
            meta:
              message.role === "assistant"
                ? `policy: ${message.policyDecision || "n/a"} · model: ${message.model || "n/a"} · risk: ${message.riskScore ?? 0}`
                : undefined,
          }))
        : [{ role: "system", content: "This chat has no messages yet." }];
      this.scrollToBottom();
    });
  }

  private scrollToBottom() {
    setTimeout(() => {
      const el = this.messagesEl?.nativeElement;
      if (el) el.scrollTo({ top: el.scrollHeight, behavior: "smooth" });
    });
  }
}
