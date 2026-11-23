import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ApiService } from '../core/api.service';
import { User, UserRole } from '../core/models';
import { problemMessage } from '../core/problem';
import { SessionService } from '../core/session.service';

const ROLE_LABELS: Record<UserRole, string> = {
  Claimant: 'Claimants',
  ClaimsOfficer: 'Claims officers',
  Manager: 'Managers',
};

/** Demo "sign in as" screen: stands in for real authentication. */
@Component({
  selector: 'app-login',
  template: `
    <section class="card narrow">
      <h1>Sign in as</h1>
      <p class="muted">Demo only: choose a seeded user. Real authentication is out of scope.</p>

      @if (error()) { <p class="error">{{ error() }}</p> }

      @for (group of groups(); track group.role) {
        <h2>{{ group.label }}</h2>
        <div class="user-list">
          @for (user of group.users; track user.id) {
            <button type="button" class="user" (click)="choose(user)">
              <strong>{{ user.fullName }}</strong>
              <span class="muted">{{ user.email }}</span>
            </button>
          }
        </div>
      }
    </section>
  `,
})
export class LoginPage implements OnInit {
  private readonly api = inject(ApiService);
  private readonly session = inject(SessionService);
  private readonly router = inject(Router);

  private readonly users = signal<User[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly groups = computed(() =>
    (Object.keys(ROLE_LABELS) as UserRole[]).map((role) => ({
      role,
      label: ROLE_LABELS[role],
      users: this.users().filter((u) => u.role === role),
    })),
  );

  ngOnInit(): void {
    this.api.users().subscribe({
      next: (users) => this.users.set(users),
      error: (err) => this.error.set(problemMessage(err)),
    });
  }

  protected choose(user: User): void {
    this.session.signIn(user);
    this.router.navigate([user.role === 'Manager' ? '/dashboard' : '/claims']);
  }
}
