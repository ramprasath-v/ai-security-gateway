import { Component, inject } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { finalize } from "rxjs";
import { ApiService } from "../../core/api.service";
import { SecurityPolicy, SecurityPolicyRequest } from "../../core/models";

@Component({
  standalone: true,
  imports: [FormsModule],
  templateUrl: "./policies.component.html",
})
export class PoliciesComponent {
  private api = inject(ApiService);
  policies: SecurityPolicy[] = [];
  loading = false;
  saving = false;
  error?: string;
  editingId?: string;
  findingTypes = [
    "SSN",
    "Email",
    "Phone",
    "API Key",
    "JWT",
    "CreditCard",
    "OpenAI API Key",
    "AWS Access Key",
    "GitHub Token",
    "Database Connection String",
    "Private Key",
    "Secret",
  ];
  actions: SecurityPolicyRequest["action"][] = ["Allow", "Mask", "Block"];
  form: SecurityPolicyRequest = this.emptyForm();

  ngOnInit() {
    this.loadPolicies();
  }

  loadPolicies() {
    this.loading = true;
    this.error = undefined;
    this.api
      .policies()
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: (policies) => (this.policies = policies),
        error: (error) =>
          (this.error = error?.error?.error || "Failed to load policies."),
      });
  }

  save() {
    const request = {
      ...this.form,
      name: this.form.name.trim(),
      description: this.form.description?.trim(),
      findingType: this.form.findingType.trim(),
      riskScore: Number(this.form.riskScore),
    };
    if (!request.name || !request.findingType) return;

    this.saving = true;
    this.error = undefined;
    const save$ = this.editingId
      ? this.api.updatePolicy(this.editingId, request)
      : this.api.createPolicy(request);

    save$.pipe(finalize(() => (this.saving = false))).subscribe({
      next: () => {
        this.cancelEdit();
        this.loadPolicies();
      },
      error: (error) =>
        (this.error = error?.error?.error || "Failed to save policy."),
    });
  }

  edit(policy: SecurityPolicy) {
    this.editingId = policy.id;
    this.form = {
      name: policy.name,
      description: policy.description,
      findingType: policy.findingType,
      action: policy.action,
      riskScore: policy.riskScore,
      enabled: policy.enabled,
    };
  }

  delete(policy: SecurityPolicy) {
    this.error = undefined;
    this.api.deletePolicy(policy.id).subscribe({
      next: () => this.loadPolicies(),
      error: (error) =>
        (this.error = error?.error?.error || "Failed to delete policy."),
    });
  }

  cancelEdit() {
    this.editingId = undefined;
    this.form = this.emptyForm();
  }

  actionTone(action: string) {
    return action === "Block" ? "danger" : action === "Mask" ? "warning" : "success";
  }

  private emptyForm(): SecurityPolicyRequest {
    return {
      name: "",
      description: "",
      findingType: "SSN",
      action: "Block",
      riskScore: 80,
      enabled: true,
    };
  }
}
