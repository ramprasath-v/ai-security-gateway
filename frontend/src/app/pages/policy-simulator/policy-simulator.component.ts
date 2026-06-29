import { Component, inject } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { RouterLink } from "@angular/router";
import { finalize } from "rxjs";
import { ApiService } from "../../core/api.service";
import { PolicySimulationResponse } from "../../core/models";

@Component({
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: "./policy-simulator.component.html",
})
export class PolicySimulatorComponent {
  private api = inject(ApiService);
  prompt = "";
  loading = false;
  result?: PolicySimulationResponse;
  error?: string;

  simulate() {
    const prompt = this.prompt.trim();
    if (!prompt) return;

    this.loading = true;
    this.error = undefined;
    this.api
      .simulatePolicy(prompt)
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: (result) => (this.result = result),
        error: (error) => {
          this.result = undefined;
          this.error = error?.error?.error || "Policy simulation failed.";
        },
      });
  }

  useSecretSample() {
    this.prompt =
      "Review this config: OPENAI_API_KEY=sk-proj-abc1234567890abcdefghijklmnopqrstuvwxyz and email admin@example.com";
  }

  useMaskSample() {
    this.prompt =
      "Draft a reminder for Alex at alex@example.com or 415-555-0182 about their appointment.";
  }

  useSafeSample() {
    this.prompt = "Summarize password reset steps in 3 bullet points.";
  }

  decisionTone(decision?: string) {
    return decision === "Block"
      ? "danger"
      : decision === "Mask"
        ? "warning"
        : "success";
  }
}
