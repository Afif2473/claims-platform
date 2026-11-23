import { DatePipe, DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { ApiService } from '../core/api.service';
import { ClaimDetail } from '../core/models';
import { problemMessage } from '../core/problem';
import { SessionService } from '../core/session.service';
import { StatusBadge } from '../core/status-badge';

/**
 * One claim: details, decision, information requests, timeline, and the actions the current
 * user may take. The buttons shown are a convenience. The API enforces every rule, and any
 * refusal (403/409) is shown to the user. On a 409 the claim is reloaded so the user sees the latest version.
 */
@Component({
  selector: 'app-claim-detail',
  imports: [RouterLink, FormsModule, DatePipe, DecimalPipe, StatusBadge],
  template: `
    <a routerLink="/claims" class="back">← Back to claims</a>

    @if (loadError()) { <p class="error">{{ loadError() }}</p> }

    @if (claim(); as c) {
      <header class="page-header">
        <div>
          <h1>{{ c.claimNumber }}</h1>
          <p class="muted">{{ c.claimType }} · {{ c.marketCode }} · submitted {{ c.createdAt | date: 'd MMM y, HH:mm' }}</p>
        </div>
        <app-status-badge [status]="c.status" />
      </header>

      @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
      @if (notice()) { <p class="notice" role="status">{{ notice() }}</p> }

      <div class="detail-grid">
        <div class="stack">
          <section class="card">
            <h2>Incident</h2>
            <dl class="facts">
              <dt>Claimant</dt><dd>{{ c.claimantName }}</dd>
              <dt>Date</dt><dd>{{ c.incidentDate | date: 'd MMM y' }}</dd>
              <dt>Location</dt><dd>{{ c.incidentLocation }}</dd>
              <dt>Officer</dt><dd>{{ c.assignedOfficerName ?? 'Not yet assigned' }}</dd>
            </dl>
            <p>{{ c.description }}</p>
          </section>

          <section class="card">
            <h2>Amounts</h2>
            <dl class="facts">
              <dt>Claimed</dt><dd>{{ c.currencyCode }} {{ c.claimedAmount | number: '1.2-2' }}</dd>
              @if (session.isStaff()) {
                <dt>Reserve</dt>
                <dd>{{ c.reserveAmount === null ? 'Not set' : c.currencyCode + ' ' + (c.reserveAmount | number: '1.2-2') }}</dd>
              }
              <dt>Approved</dt>
              <dd>{{ c.approvedAmount === null ? '—' : c.currencyCode + ' ' + (c.approvedAmount | number: '1.2-2') }}</dd>
            </dl>
          </section>

          @if (c.decidedAt) {
            <section class="card decision" [attr.data-status]="c.status">
              <h2>Decision</h2>
              <p><strong>{{ c.status === 'Rejected' ? 'Rejected' : 'Approved' }}</strong> on {{ c.decidedAt | date: 'd MMM y' }}</p>
              <p>{{ c.decisionReason }}</p>
            </section>
          }

          @if (c.informationRequests.length) {
            <section class="card">
              <h2>Information requests</h2>
              @for (r of c.informationRequests; track r.id) {
                <div class="request">
                  <p><strong>{{ r.requestedByName }} asked</strong>&nbsp;<span class="muted">({{ r.createdAt | date: 'd MMM, HH:mm' }})</span></p>
                  <p>{{ r.question }}</p>
                  @if (r.isAnswered) {
                    <p class="answer"><strong>Answer:</strong> {{ r.response }}</p>
                  } @else if (openRequestId() === r.id) {
                    <label>
                      Your answer
                      <textarea rows="3" [(ngModel)]="responseText" maxlength="2000"></textarea>
                    </label>
                    <button type="button" class="primary" [disabled]="busy() || !responseText.trim()"
                            (click)="run(api.respond(c, r.id, responseText.trim()), 'Thanks, your answer has been sent.')">
                      Send answer
                    </button>
                  } @else {
                    <p class="muted">Awaiting answer.</p>
                  }
                </div>
              }
            </section>
          }

          <section class="card">
            <h2>Timeline</h2>
            <ol class="timeline">
              @for (a of c.activities; track a.id) {
                <li>
                  <span class="muted">{{ a.createdAt | date: 'd MMM y, HH:mm' }}</span>
                  <span>{{ a.description }}</span>
                </li>
              }
            </ol>
          </section>
        </div>

        @if (session.isOfficer()) {
          <aside class="card stack actions-panel">
            <h2>Actions</h2>

            @if (c.status === 'Submitted') {
              <button type="button" class="primary" [disabled]="busy()" (click)="run(api.pickUp(c), 'Claim picked up.')">
                Pick up this claim
              </button>
            } @else if (!assignedToMe()) {
              <p class="muted">Only the assigned officer ({{ c.assignedOfficerName }}) can act on this claim.</p>
            }

            @if (assignedToMe() && (c.status === 'UnderReview' || c.status === 'AdditionalInformationRequired')) {
              <fieldset>
                <legend>Reserve (estimated cost)</legend>
                <input type="number" min="0.01" step="0.01" [(ngModel)]="reserveAmount" />
                <button type="button" [disabled]="busy() || !(reserveAmount > 0)"
                        (click)="run(api.setReserve(c, reserveAmount), 'Reserve updated.')">Set reserve</button>
              </fieldset>
            }

            @if (assignedToMe() && c.status === 'UnderReview') {
              <fieldset>
                <legend>Ask the claimant</legend>
                <textarea rows="3" [(ngModel)]="question" maxlength="1000"></textarea>
                <button type="button" [disabled]="busy() || question.trim().length < 5"
                        (click)="run(api.requestInformation(c, question.trim()), 'Question sent to the claimant.')">Request information</button>
              </fieldset>

              <fieldset>
                <legend>Approve</legend>
                <label>Amount ({{ c.currencyCode }}) <input type="number" min="0.01" step="0.01" [(ngModel)]="approvedAmount" /></label>
                <label>Reason <textarea rows="2" [(ngModel)]="approveReason" maxlength="1000"></textarea></label>
                <button type="button" class="primary" [disabled]="busy() || !(approvedAmount > 0) || approveReason.trim().length < 5"
                        (click)="run(api.approve(c, approvedAmount, approveReason.trim()), 'Claim approved.')">Approve</button>
              </fieldset>
            }

            @if (assignedToMe() && (c.status === 'UnderReview' || c.status === 'AdditionalInformationRequired')) {
              <fieldset>
                <legend>Reject</legend>
                <label>Reason <textarea rows="2" [(ngModel)]="rejectReason" maxlength="1000"></textarea></label>
                <button type="button" class="danger" [disabled]="busy() || rejectReason.trim().length < 5"
                        (click)="run(api.reject(c, rejectReason.trim()), 'Claim rejected.')">Reject</button>
              </fieldset>
            }

            @if (assignedToMe() && c.status === 'Approved') {
              <button type="button" class="primary" [disabled]="busy()" (click)="run(api.settle(c), 'Claim settled.')">
                Record payment (settle)
              </button>
            }

            @if (c.status === 'Rejected' || c.status === 'Settled') {
              <p class="muted">This claim is closed.</p>
            }
          </aside>
        }
      </div>
    }
  `,
})
export class ClaimDetailPage implements OnInit {
  /** Route parameter :id, bound by withComponentInputBinding(). */
  readonly id = input.required<string>();

  protected readonly api = inject(ApiService);
  protected readonly session = inject(SessionService);

  protected readonly claim = signal<ClaimDetail | null>(null);
  protected readonly busy = signal(false);
  protected readonly loadError = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly notice = signal<string | null>(null);

  protected readonly assignedToMe = computed(() => this.claim()?.assignedOfficerId === this.session.user()?.id);

  /** The open request this claimant can answer, if any. */
  protected readonly openRequestId = computed(() => {
    const c = this.claim();
    if (!c || !this.session.isClaimant() || c.status !== 'AdditionalInformationRequired') return null;
    return c.informationRequests.find((r) => !r.isAnswered)?.id ?? null;
  });

  // Form fields (plain properties bound with ngModel).
  protected reserveAmount = 0;
  protected question = '';
  protected approvedAmount = 0;
  protected approveReason = '';
  protected rejectReason = '';
  protected responseText = '';

  ngOnInit(): void {
    this.reload();
  }

  protected run(action: Observable<ClaimDetail>, successMessage: string): void {
    this.busy.set(true);
    this.error.set(null);
    this.notice.set(null);
    action.subscribe({
      next: (claim) => {
        this.setClaim(claim);
        this.notice.set(successMessage);
        this.busy.set(false);
      },
      error: (err) => {
        this.busy.set(false);
        if (err instanceof HttpErrorResponse && err.status === 409) {
          // Someone else changed the claim, or the action no longer fits its state.
          // Load the latest version but keep what the user typed, so they can re-check and resubmit.
          const concurrency = (err.error as { title?: string } | null)?.title === 'Concurrency conflict';
          this.error.set(
            concurrency
              ? 'Someone else updated this claim while you were viewing it. The latest version is now shown. Check it and try again.'
              : `${problemMessage(err)} The latest version is now shown.`,
          );
          this.reload(false);
        } else {
          this.error.set(problemMessage(err));
        }
      },
    });
  }

  private reload(resetForms = true): void {
    this.api.getClaim(this.id()).subscribe({
      next: (claim) => (resetForms ? this.setClaim(claim) : this.claim.set(claim)),
      error: (err) => this.loadError.set(problemMessage(err)),
    });
  }

  /** Shows the claim and resets the action forms: amounts pre-filled with the best current figure, text cleared. */
  private setClaim(claim: ClaimDetail): void {
    this.claim.set(claim);
    this.reserveAmount = claim.reserveAmount ?? claim.claimedAmount;
    this.approvedAmount = claim.approvedAmount ?? claim.reserveAmount ?? claim.claimedAmount;
    this.question = '';
    this.approveReason = '';
    this.rejectReason = '';
    this.responseText = '';
  }
}
