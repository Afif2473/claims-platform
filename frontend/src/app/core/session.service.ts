import { Injectable, computed, signal } from '@angular/core';
import { User } from './models';

const STORAGE_KEY = 'claims.currentUser';

/**
 * Who is "signed in". Mirrors the backend's simulated identity: picking a seeded user here
 * makes every API call carry that user's id in X-User-Id (see userIdInterceptor).
 * Kept in localStorage so a page refresh keeps the same user.
 */
@Injectable({ providedIn: 'root' })
export class SessionService {
  readonly user = signal<User | null>(this.load());
  readonly isStaff = computed(() => {
    const role = this.user()?.role;
    return role === 'ClaimsOfficer' || role === 'Manager';
  });
  readonly isOfficer = computed(() => this.user()?.role === 'ClaimsOfficer');
  readonly isClaimant = computed(() => this.user()?.role === 'Claimant');

  signIn(user: User): void {
    this.user.set(user);
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(user));
    } catch {
      // Storage unavailable (private mode): session lasts until refresh.
    }
  }

  signOut(): void {
    this.user.set(null);
    try {
      localStorage.removeItem(STORAGE_KEY);
    } catch {
      // ignore
    }
  }

  private load(): User | null {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      return raw ? (JSON.parse(raw) as User) : null;
    } catch {
      return null;
    }
  }
}
