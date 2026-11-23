import { DatePipe } from '@angular/common';
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { EMPTY, catchError, switchMap, timer } from 'rxjs';
import { ApiService } from '../core/api.service';
import { AppNotification } from '../core/models';
import { problemMessage } from '../core/problem';

const POLL_MS = 5000;

/**
 * Updates about the claimant's claims. Notifications are created asynchronously by the Kafka
 * consumer, so this page polls the API every few seconds while it is open. That is the frontend
 * side of the "async claims processing" question. Server push (SSE/WebSocket) would replace polling at scale.
 */
@Component({
  selector: 'app-notifications',
  imports: [DatePipe, RouterLink],
  template: `
    <header class="page-header">
      <h1>Updates</h1>
      <span class="muted">Refreshes every {{ pollSeconds }} seconds</span>
    </header>

    @if (error()) { <p class="error">{{ error() }}</p> }

    @if (items().length === 0) {
      <p class="muted empty">No updates yet.</p>
    } @else {
      <ul class="notifications">
        @for (n of items(); track n.id) {
          <li class="card">
            <a [routerLink]="['/claims', n.claimId]">{{ n.message }}</a>
            <span class="muted">{{ n.createdAt | date: 'd MMM y, HH:mm' }}</span>
          </li>
        }
      </ul>
    }
  `,
})
export class NotificationsPage implements OnInit {
  private readonly api = inject(ApiService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly pollSeconds = POLL_MS / 1000;
  protected readonly items = signal<AppNotification[]>([]);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    timer(0, POLL_MS)
      .pipe(
        // Errors are caught per request: an error reaching the outer stream would stop polling for good.
        switchMap(() =>
          this.api.notifications().pipe(
            catchError((err) => {
              this.error.set(problemMessage(err));
              return EMPTY;
            }),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((items) => {
        this.items.set(items);
        this.error.set(null);
      });
  }
}
