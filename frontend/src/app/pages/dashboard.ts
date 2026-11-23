import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ApiService } from '../core/api.service';
import { Dashboard } from '../core/models';
import { problemMessage } from '../core/problem';
import { StatusBadge } from '../core/status-badge';

/** Team workload, performance and outstanding liability. Staff only (the API returns 403 to claimants). */
@Component({
  selector: 'app-dashboard',
  imports: [DatePipe, DecimalPipe, StatusBadge],
  template: `
    <header class="page-header">
      <h1>Team dashboard</h1>
      <button type="button" (click)="load()">Refresh</button>
    </header>

    @if (error()) { <p class="error">{{ error() }}</p> }

    @if (data(); as d) {
      <p class="muted">As of {{ d.generatedAt | date: 'd MMM y, HH:mm:ss' }}</p>

      <section class="tiles">
        <div class="tile">
          <span class="tile-label">Waiting to be picked up</span>
          <span class="tile-value">{{ d.queue.unassigned }}</span>
          <span class="muted">
            @if (d.queue.oldestUnassignedDays !== null) { oldest {{ d.queue.oldestUnassignedDays }} days } @else { queue is empty }
          </span>
        </div>
        @for (s of d.claimsByStatus; track s.status) {
          <div class="tile">
            <span class="tile-label"><app-status-badge [status]="s.status" /></span>
            <span class="tile-value">{{ s.count }}</span>
          </div>
        }
      </section>

      <section class="card">
        <h2>Outstanding liability exposure</h2>
        <p class="muted">Open claims only. Uses approved amount, then reserve, then claimed amount. Shown per market, with no currency conversion.</p>
        @if (d.exposure.length === 0) {
          <p class="muted">No open claims.</p>
        } @else {
          <div class="table-wrap">
            <table>
              <thead>
                <tr><th>Market</th><th class="num">Open claims</th><th class="num">Claimed</th><th class="num">Exposure</th><th class="num">Not yet reserved</th></tr>
              </thead>
              <tbody>
                @for (e of d.exposure; track e.marketCode) {
                  <tr>
                    <td>{{ e.marketCode }}</td>
                    <td class="num">{{ e.openClaims }}</td>
                    <td class="num">{{ e.currencyCode }} {{ e.totalClaimed | number: '1.2-2' }}</td>
                    <td class="num"><strong>{{ e.currencyCode }} {{ e.outstandingExposure | number: '1.2-2' }}</strong></td>
                    <td class="num">{{ e.unreservedClaims }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </section>

      <section class="card">
        <h2>Officer workload and performance</h2>
        <div class="table-wrap">
          <table>
            <thead>
              <tr><th>Officer</th><th class="num">Open</th><th class="num">Waiting on claimant</th><th class="num">Approved</th><th class="num">Rejected</th><th class="num">Avg days to decision</th></tr>
            </thead>
            <tbody>
              @for (o of d.officers; track o.officerId) {
                <tr>
                  <td>{{ o.officerName }}</td>
                  <td class="num">{{ o.openClaims }}</td>
                  <td class="num">{{ o.awaitingClaimant }}</td>
                  <td class="num">{{ o.approved }}</td>
                  <td class="num">{{ o.rejected }}</td>
                  <td class="num">{{ o.averageDaysToDecision ?? '—' }}</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>
    }
  `,
})
export class DashboardPage implements OnInit {
  private readonly api = inject(ApiService);
  protected readonly data = signal<Dashboard | null>(null);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    this.error.set(null);
    this.api.dashboard().subscribe({
      next: (d) => this.data.set(d),
      error: (err) => this.error.set(problemMessage(err)),
    });
  }
}
