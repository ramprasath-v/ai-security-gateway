import { Component, ElementRef, ViewChild, inject } from "@angular/core";
import { DatePipe } from "@angular/common";
import { FormsModule } from "@angular/forms";
import { finalize } from "rxjs";
import { ApiService } from "../../core/api.service";
import { AiApplication, ChatResponse, ChatSession } from "../../core/models";

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
  templateUrl: "./chat.component.html",
})
export class ChatComponent {
  private api = inject(ApiService);
  @ViewChild("messagesEl") private messagesEl?: ElementRef<HTMLElement>;
  prompt = "";
  provider = "Ollama";
  model = "llama3.2:1b";
  selectedApplicationId?: string;
  applications: AiApplication[] = [];
  loading = false;
  sessions: ChatSession[] = [];
  activeSessionId?: string;
  messages: ChatMessage[] = [
    {
      role: "system",
      content:
        "Welcome. I scan prompts before forwarding allowed requests to model.",
    },
  ];

  constructor() {
    this.loadApplications();
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
      .sendChat({
        message,
        provider: this.provider,
        model: this.model,
        sessionId: this.activeSessionId,
        applicationId: this.selectedApplicationId,
      })
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
      "Explain the purpose of an API gateway in three short bullet points.";
  }

  useRisky() {
    this.selectApplicationByName("HR Assistant");
    this.prompt =
      "Send email to hr@company.com about employee SSN 123-45-6789 and password=Secret123";
  }

  useFinancePrompt() {
    this.selectApplicationByName("Finance Assistant");
    this.prompt =
      "Review this payment note for card 4111 1111 1111 1111 before sending it to accounting.";
  }

  useCodePrompt() {
    this.selectApplicationByName("Code Assistant");
    this.prompt =
      "Review this TypeScript function for readability and suggest two safe improvements.";
  }

  useServiceDeskPrompt() {
    this.selectApplicationByName("Service Desk Agent");
    this.prompt =
      "Create a service desk ticket for a user who needs help resetting their account password.";
  }

  useSemanticSeed() {
    this.prompt = "Summarize password reset steps in 3 bullet points.";
  }

  useSemanticFollowup() {
    this.prompt =
      "Explain how to reset a forgotten password in three simple steps.";
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
    const application = response.applicationName
      ? `app: ${response.applicationName}`
      : "app: unknown";
    return `${response.status} · ${cache} · ${application} · policy: ${response.policyAction} · provider: ${response.provider} · model: ${response.model} · risk: ${response.riskScore} · ${tokens} · ${gateway} · ${policy} · ${ollama} · ${db} · ${Math.round(clientElapsedMs)} ms browser`;
  }

  private loadApplications() {
    this.api.applications().subscribe((applications) => {
      this.applications = applications.filter((application) => application.enabled);
      if (!this.selectedApplicationId) {
        this.selectedApplicationId =
          this.applications.find((application) => application.name === "AI Chat Portal")?.id ??
          this.applications[0]?.id;
      }
    });
  }

  private selectApplicationByName(name: string) {
    const application = this.applications.find((item) => item.name === name);
    if (application) this.selectedApplicationId = application.id;
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
