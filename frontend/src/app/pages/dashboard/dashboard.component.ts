import { Component, inject } from "@angular/core";
import { AsyncPipe, DatePipe, DecimalPipe } from "@angular/common";
import { RouterLink } from "@angular/router";
import { ApiService } from "../../core/api.service";
import { ChatHistoryItem, DashboardRequestDetail } from "../../core/models";

@Component({
  standalone: true,
  imports: [AsyncPipe, DatePipe, DecimalPipe, RouterLink],
  templateUrl: "./dashboard.component.html",
})
export class DashboardComponent {
  private api = inject(ApiService);
  dashboard$ = this.api.dashboard();
  selectedRequest?: DashboardRequestDetail;
  detailLoading = false;
  detailError?: string;

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

  openDetails(item: ChatHistoryItem) {
    this.detailLoading = true;
    this.detailError = undefined;
    this.selectedRequest = undefined;
    this.api.dashboardRequest(item.id).subscribe({
      next: (detail) => {
        this.selectedRequest = detail;
        this.detailLoading = false;
      },
      error: (error) => {
        this.detailError = error?.error?.error || "Request details could not be loaded.";
        this.detailLoading = false;
      },
    });
  }

  closeDetails() {
    this.selectedRequest = undefined;
    this.detailError = undefined;
    this.detailLoading = false;
  }

  cacheLabel(item: ChatHistoryItem | DashboardRequestDetail) {
    if (item.exactCacheHit) return "Exact";
    if (item.semanticCacheHit) return "Semantic";
    if (item.cacheHit) return "Hit";
    return "Miss";
  }

  cacheTone(item: ChatHistoryItem | DashboardRequestDetail) {
    return item.cacheHit ? "pill hit" : "pill miss";
  }

  tokenSummary(item: DashboardRequestDetail) {
    return `${item.llmTokensUsed} used · ${item.tokensSaved} saved · ${item.cachedTokensServed} cached`;
  }
}
