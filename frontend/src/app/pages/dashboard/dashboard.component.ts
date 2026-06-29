import { Component, inject } from "@angular/core";
import { AsyncPipe, DatePipe, DecimalPipe } from "@angular/common";
import { RouterLink } from "@angular/router";
import { ApiService } from "../../core/api.service";

@Component({
  standalone: true,
  imports: [AsyncPipe, DatePipe, DecimalPipe, RouterLink],
  templateUrl: "./dashboard.component.html",
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
