import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../core/api.service';
import { AssignmentFilter, ClaimStatus, ClaimSummary, STATUS_LABELS } from '../core/models';
import { problemMessage } from '../core/problem';
import { SessionService } from '../core/session.service';
import { StatusBadge } from '../core/status-badge';

/**
 * Claimants: their own claims. Staff: the work queue, filtered by assignment and status.
 * The API decides what each role may see; this page only chooses the filters.
 */
@Component({
  selector: 'app-claim-list',
  imports: [RouterLink, FormsModule, DatePipe, DecimalPipe, StatusBadge],
  template: `
    <header class="page-header">
      <h1>{{ session.isStaff() ? 'Claims queue' : 'My claims' }}</h1>
      @if (session.isClaimant()) {
        <a class="button primary" routerLink="/claims/new">Report an incident</a>
      }
    </header>

    @if (session.isStaff()) {
      <div class="toolbar">
        <div class="tabs" role="tablist">
          @for (tab of tabs; track tab.value) {
            <button type="button" role="tab" [class.active]="assignment() === tab.value"
                    [attr.aria-selected]="assignment() === tab.value" (click)="setAssignment(tab.value)">
              {{ tab.label }}
            </button>
          }
        </div>
        <label>
          Status
          <select [ngModel]="status()" (ngModelChange)="setStatus($event)">
            <option value="">All</option>
            @for (s of statuses; track s) { <option [value]="s">{{ statusLabels[s] }}</option> }
          </select>
        </label>
      </div>
    }

    @if (error()) { <p class="error">{{ error() }}</p> }

    @if (loading()) {
      <p class="muted">Loading…</p>
    } @else if (claims().length === 0) {
      <p class="muted empty">No claims here.</p>
    } @else {
      <div class="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Claim</th>
              <th>Type</th>
              <th>Status</th>
              @if (session.isStaff()) { <th>Claimant</th><th>Officer</th> }
              <th class="num">Claimed</th>
              <th>Submitted</th>
            </tr>
          </thead>
          <tbody>
            @for (claim of claims(); track claim.id) {
              <tr>
                <td><a [routerLink]="['/claims', claim.id]">{{ claim.claimNumber }}</a></td>
                <td>{{ claim.claimType }}</td>
                <td><app-status-badge [status]="claim.status" /></td>
                @if (session.isStaff()) {
                  <td>{{ claim.claimantName }}</td>
                  <td>{{ claim.assignedOfficerName ?? '—' }}</td>
                }
                <td class="num">{{ claim.currencyCode }} {{ claim.claimedAmount | number: '1.2-2' }}</td>
                <td>{{ claim.createdAt | date: 'd MMM y' }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
  `,
})
export class ClaimListPage implements OnInit {
  private readonly api = inject(ApiService);
  protected readonly session = inject(SessionService);

  protected readonly tabs: { value: AssignmentFilter; label: string }[] = [
    { value: 'Unassigned', label: 'Unassigned' },
    { value: 'Mine', label: 'Assigned to me' },
    { value: 'All', label: 'All claims' },
  ];
  protected readonly statuses = Object.keys(STATUS_LABELS) as ClaimStatus[];
  protected readonly statusLabels = STATUS_LABELS;

  protected readonly claims = signal<ClaimSummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  // Officers start on the incoming queue; managers have no queue of their own, so they see everything.
  protected readonly assignment = signal<AssignmentFilter>(this.session.isOfficer() ? 'Unassigned' : 'All');
  protected readonly status = signal<ClaimStatus | ''>('');

  ngOnInit(): void {
    this.load();
  }

  protected setAssignment(value: AssignmentFilter): void {
    this.assignment.set(value);
    this.load();
  }

  protected setStatus(value: ClaimStatus | ''): void {
    this.status.set(value);
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.listClaims({ assignment: this.assignment(), status: this.status() }).subscribe({
      next: (claims) => {
        this.claims.set(claims);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(problemMessage(err));
        this.loading.set(false);
      },
    });
  }
}
