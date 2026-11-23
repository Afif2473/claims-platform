import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ApiService } from '../core/api.service';
import { ClaimType, MARKETS } from '../core/models';
import { problemMessage } from '../core/problem';

function localDate(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

/**
 * First notice of loss. Client-side validation mirrors the API's rules for fast feedback,
 * but the API re-validates everything and its message is shown if it disagrees.
 */
@Component({
  selector: 'app-new-claim',
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <section class="card narrow">
      <h1>Report an incident</h1>

      <form [formGroup]="form" (ngSubmit)="submit()">
        <label>
          Type of claim
          <select formControlName="claimType">
            <option value="Motor">Motor</option>
            <option value="Property">Property</option>
          </select>
        </label>

        <label>
          Market
          <select formControlName="marketCode">
            @for (m of markets; track m) { <option [value]="m">{{ m }}</option> }
          </select>
        </label>

        <label>
          Date of incident
          <input type="date" formControlName="incidentDate" [max]="today" />
        </label>

        <label>
          Where did it happen?
          <input type="text" formControlName="incidentLocation" maxlength="250" />
        </label>

        <label>
          What happened?
          <textarea formControlName="description" rows="4" maxlength="2000"></textarea>
          <small class="muted">At least 10 characters.</small>
        </label>

        <label>
          Amount claimed
          <input type="number" formControlName="claimedAmount" min="0.01" step="0.01" />
          <small class="muted">Currency is set by the market.</small>
        </label>

        @if (error()) { <p class="error">{{ error() }}</p> }

        <div class="actions">
          <a class="button" routerLink="/claims">Cancel</a>
          <button type="submit" class="primary" [disabled]="form.invalid || submitting()">
            {{ submitting() ? 'Submitting…' : 'Submit claim' }}
          </button>
        </div>
      </form>
    </section>
  `,
})
export class NewClaimPage {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);

  protected readonly markets = MARKETS;
  // Local calendar date (toISOString would give the UTC date, which is "yesterday" before 8am in UTC+8).
  protected readonly today = localDate(new Date());
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = inject(FormBuilder).nonNullable.group({
    claimType: ['Motor' as ClaimType, Validators.required],
    marketCode: ['SG', Validators.required],
    incidentDate: [this.today, Validators.required],
    incidentLocation: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(250)]],
    description: ['', [Validators.required, Validators.minLength(10), Validators.maxLength(2000)]],
    claimedAmount: [0, [Validators.required, Validators.min(0.01)]],
  });

  protected submit(): void {
    if (this.form.invalid) return;
    const value = this.form.getRawValue();
    this.submitting.set(true);
    this.error.set(null);

    this.api
      .submitClaim({
        ...value,
        // Start of the chosen day in the user's timezone. Never in the future, even for "today"
        // (noon UTC would be in the future for a morning submission in UTC+8 and the API would reject it).
        incidentDate: new Date(`${value.incidentDate}T00:00:00`).toISOString(),
      })
      .subscribe({
        next: (claim) => this.router.navigate(['/claims', claim.id]),
        error: (err) => {
          this.error.set(problemMessage(err));
          this.submitting.set(false);
        },
      });
  }
}
