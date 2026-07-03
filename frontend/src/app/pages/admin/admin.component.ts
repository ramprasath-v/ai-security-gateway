import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { ClearSemanticCacheResponse, ResetDemoResponse } from '../../core/models';

@Component({
  selector: 'app-admin',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './admin.component.html'
})
export class AdminComponent {
  private readonly api = inject(ApiService);

  readonly requiredConfirmation = 'RESET DEMO';
  confirmation = '';
  resetting = false;
  clearingCache = false;
  error = '';
  result: ResetDemoResponse | null = null;
  cacheResult: ClearSemanticCacheResponse | null = null;

  resetDemoEnvironment() {
    if (this.confirmation !== this.requiredConfirmation || this.resetting) {
      return;
    }

    const confirmed = window.confirm(
      'Reset the Security Gateway demo environment? This clears semantic cache, chat history, and request logs.'
    );
    if (!confirmed) {
      return;
    }

    this.resetting = true;
    this.error = '';
    this.result = null;
    this.cacheResult = null;

    this.api.resetDemoEnvironment(this.confirmation).subscribe({
      next: (response) => {
        this.result = response;
        this.confirmation = '';
      },
      error: (err) => {
        this.error = err?.error?.message ?? 'Could not reset the Security Gateway demo environment.';
      },
      complete: () => {
        this.resetting = false;
      }
    });
  }

  clearSemanticCache() {
    if (this.confirmation !== this.requiredConfirmation || this.clearingCache) {
      return;
    }

    const confirmed = window.confirm('Clear only the Security Gateway semantic cache?');
    if (!confirmed) {
      return;
    }

    this.clearingCache = true;
    this.error = '';
    this.result = null;
    this.cacheResult = null;

    this.api.clearSemanticCache(this.confirmation).subscribe({
      next: (response) => {
        this.cacheResult = response;
        this.confirmation = '';
      },
      error: (err) => {
        this.error = err?.error?.message ?? 'Could not clear the semantic cache.';
      },
      complete: () => {
        this.clearingCache = false;
      }
    });
  }
}
