import { Component, inject } from '@angular/core';
import { AsyncPipe, DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';

@Component({
  standalone: true,
  imports: [AsyncPipe, DatePipe, DecimalPipe, RouterLink],
  template: `
    <section class="page-head">
      <div>
        <h2>Gateway Dashboard</h2>
        <p>Only real requests logged by the SQLite database are shown here.</p>
      </div>
      <a routerLink="/" class="button secondary">Open chat</a>
    </section>

    @if(dashboard$ | async; as d) {
      <div class="stats">
        <article class="stat"><span class="stat-icon">↗</span><div><b>{{d.totalRequests}}</b><span>Total requests</span></div></article>
        <article class="stat"><span class="stat-icon success">✓</span><div><b>{{d.successCount}}</b><span>Successful</span></div></article>
        <article class="stat"><span class="stat-icon danger">■</span><div><b>{{d.blockedCount}}</b><span>Blocked</span></div></article>
        <article class="stat"><span class="stat-icon warning">!</span><div><b>{{d.policyViolations}}</b><span>Policy actions</span></div></article>
        <article class="stat"><span class="stat-icon danger">●</span><div><b>{{d.sensitiveDataFindings}}</b><span>Sensitive findings</span></div></article>
        <article class="stat"><span class="stat-icon">⏱</span><div><b>{{d.averageResponseTimeMs | number:'1.0-0'}} ms</b><span>Average response</span></div></article>
        <article class="stat"><span class="stat-icon">⚡</span><div><b>{{d.fastestRequestMs ?? 0}} ms</b><span>Fastest request</span></div></article>
        <article class="stat"><span class="stat-icon warning">⌛</span><div><b>{{d.slowestRequestMs ?? 0}} ms</b><span>Slowest request</span></div></article>
        <article class="stat"><span class="stat-icon">◌</span><div><b>{{d.totalTokensUsed}}</b><span>Total tokens</span></div></article>
        <article class="stat"><span class="stat-icon">◇</span><div><b>{{d.averageTokensPerRequest | number:'1.0-0'}}</b><span>Avg tokens/request</span></div></article>
        <article class="stat"><span class="stat-icon success">↩</span><div><b>{{d.cacheHitCount}}</b><span>Cache hits</span></div></article>
        <article class="stat"><span class="stat-icon success">=</span><div><b>{{d.exactCacheHitCount}}</b><span>Exact hits</span></div></article>
        <article class="stat"><span class="stat-icon success">≈</span><div><b>{{d.semanticCacheHitCount}}</b><span>Semantic hits</span></div></article>
        <article class="stat"><span class="stat-icon">↪</span><div><b>{{d.cacheMissCount}}</b><span>Cache misses</span></div></article>
      </div>
      <p class="dashboard-note">Last request: {{d.lastRequestTime ? (d.lastRequestTime | date:'medium') : 'No requests yet'}}</p>

      <section class="dashboard-grid">
        <article class="panel">
          <h3>Model usage</h3>
          @for(item of d.modelUsage; track item.model) {
            <div class="metric-row"><span>{{item.model}}</span><b>{{item.count}}</b></div>
          } @empty {
            <p class="empty">No model usage yet.</p>
          }
        </article>

        <article class="panel">
          <h3>Policy decisions</h3>
          @for(item of d.policyActions; track item.action) {
            <div class="metric-row"><span>{{item.action}}</span><b>{{item.count}}</b></div>
          } @empty {
            <p class="empty">No policy actions yet.</p>
          }
        </article>

        <article class="panel">
          <h3>Sensitive data findings</h3>
          @for(item of d.findingTypes; track item.type) {
            <div class="metric-row"><span>{{item.type}}</span><b>{{item.count}}</b></div>
          } @empty {
            <p class="empty">No sensitive data findings yet.</p>
          }
        </article>

        <article class="panel">
          <h3>Recent requests</h3>
          <div class="table-wrap">
            <table>
              <thead><tr><th>Local time</th><th>Prompt preview</th><th>Model</th><th>Policy</th><th>Risk</th><th>Total ms</th><th>Ollama ms</th><th>Prompt tokens</th><th>Response tokens</th><th>Total tokens</th><th>Cache</th><th>Similarity</th><th>Status / error</th></tr></thead>
              <tbody>
                @for(item of d.recentRequests; track item.id) {
                  <tr>
                    <td>{{item.timestampUtc | date:'short'}}</td>
                    <td class="preview">{{item.promptPreview}}</td>
                    <td>{{item.model}}</td>
                    <td>{{item.policyAction}}</td>
                    <td>{{item.riskScore}}</td>
                    <td>{{item.totalDurationMs ?? 0}}</td>
                    <td>{{item.ollamaDurationMs ?? '—'}}</td>
                    <td>{{item.promptTokens ?? '—'}}</td>
                    <td>{{item.responseTokens ?? '—'}}</td>
                    <td>{{item.totalTokens ?? '—'}}</td>
                    <td>
                      <span [class]="item.cacheHit ? 'pill hit' : 'pill miss'">
                        {{item.exactCacheHit ? 'exact' : item.semanticCacheHit ? 'semantic' : item.cacheHit ? 'hit' : 'miss'}}
                      </span>
                    </td>
                    <td>{{item.similarityScore == null ? '—' : (item.similarityScore | number:'1.2-3')}}</td>
                    <td class="error-cell">{{item.errorMessage || item.status}}</td>
                  </tr>
                } @empty {
                  <tr><td colspan="13" class="empty">No requests yet.</td></tr>
                }
              </tbody>
            </table>
          </div>
        </article>
      </section>
    }`
})
export class DashboardComponent {
  private api = inject(ApiService);
  dashboard$ = this.api.dashboard();
}
