import { Component, inject } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { finalize } from "rxjs";
import { ApiService } from "../../core/api.service";
import { AiApplication, AiApplicationRequest } from "../../core/models";

@Component({
  standalone: true,
  imports: [FormsModule],
  templateUrl: "./applications.component.html",
})
export class ApplicationsComponent {
  private api = inject(ApiService);
  applications: AiApplication[] = [];
  loading = false;
  saving = false;
  error?: string;
  editingId?: string;
  environments: AiApplicationRequest["environment"][] = ["Dev", "Test", "Prod"];
  form: AiApplicationRequest = this.emptyForm();

  ngOnInit() {
    this.loadApplications();
  }

  loadApplications() {
    this.loading = true;
    this.error = undefined;
    this.api
      .applications()
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: (applications) => (this.applications = applications),
        error: (error) =>
          (this.error = error?.error?.error || "Failed to load applications."),
      });
  }

  save() {
    const request = {
      ...this.form,
      name: this.form.name.trim(),
      owner: this.form.owner.trim(),
      description: this.form.description?.trim(),
      appKey: this.form.appKey?.trim(),
    };
    if (!request.name || !request.owner) return;

    this.saving = true;
    this.error = undefined;
    const save$ = this.editingId
      ? this.api.updateApplication(this.editingId, request)
      : this.api.createApplication(request);

    save$.pipe(finalize(() => (this.saving = false))).subscribe({
      next: () => {
        this.cancelEdit();
        this.loadApplications();
      },
      error: (error) =>
        (this.error = error?.error?.error || "Failed to save application."),
    });
  }

  edit(application: AiApplication) {
    this.editingId = application.id;
    this.form = {
      name: application.name,
      owner: application.owner,
      environment: application.environment,
      description: application.description,
      appKey: application.appKey,
      enabled: application.enabled,
    };
  }

  delete(application: AiApplication) {
    this.error = undefined;
    this.api.deleteApplication(application.id).subscribe({
      next: () => this.loadApplications(),
      error: (error) =>
        (this.error = error?.error?.error || "Failed to delete application."),
    });
  }

  cancelEdit() {
    this.editingId = undefined;
    this.form = this.emptyForm();
  }

  environmentTone(environment: string) {
    return environment === "Prod" ? "danger" : environment === "Test" ? "warning" : "success";
  }

  private emptyForm(): AiApplicationRequest {
    return {
      name: "",
      owner: "",
      environment: "Dev",
      description: "",
      appKey: "",
      enabled: true,
    };
  }
}
