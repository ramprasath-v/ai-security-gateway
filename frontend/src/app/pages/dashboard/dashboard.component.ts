import { Component, inject } from "@angular/core";
import { AsyncPipe, DatePipe, DecimalPipe } from "@angular/common";
import { RouterLink } from "@angular/router";
import { ApiService } from "../../core/api.service";

@Component({
  standalone: true,
  imports: [AsyncPipe, DatePipe, DecimalPipe, RouterLink],
  template: ` <section class="page-head">
      <div>
        <!-- <span class="demo-kicker">Live Demo Dashboard</span> -->
        <h2>Gateway Dashboard</h2>
        <!-- <p>Real SQLite telemetry from previous AI Gateway requests — no mock data.</p> -->
      </div>
      <a routerLink="/" class="button secondary">Open chat</a>
    </section>

    @if (dashboard$ | async; as d) {
      <div class="stats">
        <article class="stat hero">
          <span class="stat-icon">↗</span>
          <div>
            <b>{{ d.totalRequests }}</b
            ><span>Total requests</span><small>Gateway traffic</small>
          </div>
        </article>
        <article class="stat">
          <span class="stat-icon success">✓</span>
          <div>
            <b>{{ d.successCount }}</b
            ><span>Allowed</span><small>Completed requests</small>
          </div>
        </article>
        <article class="stat">
          <span class="stat-icon danger">⛔</span>
          <div>
            <b>{{ d.blockedCount }}</b
            ><span>Blocked</span><small>Stopped by policy</small>
          </div>
        </article>
        <article class="stat">
          <span class="stat-icon warning">⚠</span>
          <div>
            <b>{{ d.policyViolations }}</b
            ><span>Policy decisions</span><small>Non-allow actions</small>
          </div>
        </article>
        <article class="stat">
          <span class="stat-icon danger">●</span>
          <div>
            <b>{{ d.sensitiveDataFindings }}</b
            ><span>Sensitive findings</span><small>PII/secrets detected</small>
          </div>
        </article>
        <article class="stat">
          <span class="stat-icon">⏱</span>
          <div>
            <b>{{ d.averageResponseTimeMs | number: "1.0-0" }} ms</b
            ><span>Average response</span><small>End-to-end gateway</small>
          </div>
        </article>
        <article class="stat">
          <span class="stat-icon success">⚡</span>
          <div>
            <b>{{ d.fastestRequestMs ?? 0 }} ms</b><span>Fastest</span
            ><small>Often cache-backed</small>
          </div>
        </article>
        <article class="stat">
          <span class="stat-icon warning">⌛</span>
          <div>
            <b>{{ d.slowestRequestMs ?? 0 }} ms</b><span>Slowest</span
            ><small>Likely Ollama call</small>
          </div>
        </article>
        <article class="stat">
          <span class="stat-icon">◌</span>
          <div>
            <b>{{ d.totalTokensUsed }}</b
            ><span>LLM tokens used</span><small>Cache hits count as zero</small>
          </div>
        </article>
        <article class="stat">
          <span class="stat-icon success">↯</span>
          <div>
            <b>{{ d.totalTokensSaved }}</b
            ><span>Tokens saved</span><small>Exact + semantic cache</small>
          </div>
        </article>
        <article class="stat">
          <span class="stat-icon success">▣</span>
          <div>
            <b>{{ d.totalCachedTokensServed }}</b
            ><span>Cached tokens served</span
            ><small>Served without Ollama</small>
          </div>
        </article>
        <article class="stat">
          <span class="stat-icon">◇</span>
          <div>
            <b>{{ d.averageTokensPerRequest | number: "1.0-0" }}</b
            ><span>Avg tokens</span><small>LLM tokens only</small>
          </div>
        </article>
        <article class="stat">
          <span class="stat-icon success">↩</span>
          <div>
            <b>{{ d.cacheHitCount }}</b
            ><span>Cache hits</span><small>Exact + semantic</small>
          </div>
        </article>
        <article class="stat">
          <span class="stat-icon success">≈</span>
          <div>
            <b>{{ d.semanticCacheHitCount }}</b
            ><span>Semantic hits</span><small>Similarity matched</small>
          </div>
        </article>
      </div>
      <p class="dashboard-note">
        Last request:
        {{
          d.lastRequestTime
            ? (d.lastRequestTime | date: "medium")
            : "No requests yet"
        }}
      </p>

      <section class="dashboard-grid">
        <article class="panel">
          <h3>Model usage</h3>
          @for (item of d.modelUsage; track item.model) {
            <div class="metric-row">
              <span>{{ item.model }}</span
              ><b>{{ item.count }}</b>
            </div>
          } @empty {
            <p class="empty">No model usage yet.</p>
          }
        </article>

        <article class="panel">
          <h3>Policy decisions</h3>
          @for (item of d.policyActions; track item.action) {
            <div class="metric-row">
              <span>{{ item.action }}</span
              ><b>{{ item.count }}</b>
            </div>
          } @empty {
            <p class="empty">No policy actions yet.</p>
          }
        </article>

        <article class="panel">
          <h3>Sensitive data findings</h3>
          @for (item of d.findingTypes; track item.type) {
            <div class="metric-row">
              <span>{{ item.type }}</span
              ><b>{{ item.count }}</b>
            </div>
          } @empty {
            <p class="empty">No sensitive data findings yet.</p>
          }
        </article>

        <article class="panel">
          <h3>Recent requests</h3>
          <div class="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Local time</th>
                  <th>Prompt preview</th>
                  <th>Model</th>
                  <th>Policy</th>
                  <th>Risk</th>
                  <th>Total ms</th>
                  <th>Ollama ms</th>
                  <th>Prompt tokens</th>
                  <th>Response tokens</th>
                  <th>LLM tokens used</th>
                  <th>Tokens saved</th>
                  <th>Cached tokens served</th>
                  <th>Cache</th>
                  <th>Similarity</th>
                  <th>Status / error</th>
                </tr>
              </thead>
              <tbody>
                @for (item of d.recentRequests; track item.id) {
                  <tr>
                    <td>{{ item.timestampUtc | date: "short" }}</td>
                    <td class="preview">{{ item.promptPreview }}</td>
                    <td>{{ item.model }}</td>
                    <td>
                      <span
                        [class]="'badge ' + policyTone(item.policyAction)"
                        >{{ item.policyAction }}</span
                      >
                    </td>
                    <td>{{ item.riskScore }}</td>
                    <td [class]="durationTone(item.totalDurationMs)">
                      {{ item.totalDurationMs ?? 0 }}
                    </td>
                    <td [class]="durationTone(item.ollamaDurationMs)">
                      {{ item.ollamaDurationMs ?? "—" }}
                    </td>
                    <td>{{ item.promptTokens ?? "—" }}</td>
                    <td>{{ item.responseTokens ?? "—" }}</td>
                    <td>{{ item.llmTokensUsed }}</td>
                    <td>{{ item.tokensSaved }}</td>
                    <td>{{ item.cachedTokensServed }}</td>
                    <td>
                      <span [class]="item.cacheHit ? 'pill hit' : 'pill miss'">
                        {{
                          item.exactCacheHit
                            ? "exact"
                            : item.semanticCacheHit
                              ? "semantic"
                              : item.cacheHit
                                ? "hit"
                                : "miss"
                        }}
                      </span>
                    </td>
                    <td>
                      <span
                        [class]="
                          item.semanticCacheHit
                            ? 'similarity hot'
                            : 'similarity'
                        "
                        >{{
                          item.similarityScore == null
                            ? "—"
                            : (item.similarityScore | number: "1.2-3")
                        }}</span
                      >
                    </td>
                    <td class="error-cell">
                      {{ item.errorMessage || item.status }}
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="15" class="empty">No requests yet.</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </article>
      </section>
    }`,
})
export class DashboardComponent {
  private api = inject(ApiService);
  dashboard$ = this.api.dashboard();

  policyTone(policy: string) {
    return policy === "block"
      ? "danger"
      : policy === "mask"
        ? "warning"
        : "success";
  }

  durationTone(value?: number | null) {
    if (value == null) return "duration neutral";
    if (value > 5000) return "duration slow";
    if (value > 1000) return "duration medium";
    return "duration fast";
  }
}
