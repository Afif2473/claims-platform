import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AppNotification,
  ClaimDetail,
  ClaimListQuery,
  ClaimSummary,
  Dashboard,
  SubmitClaimRequest,
  User,
} from './models';

/**
 * Thin typed client for the Claims API. Every state-changing call sends the claim version
 * the user is looking at, so the API can reject it with 409 if someone else changed the claim.
 */
@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api';

  users(): Observable<User[]> {
    return this.http.get<User[]>(`${this.base}/users`);
  }

  listClaims(query: ClaimListQuery = {}): Observable<ClaimSummary[]> {
    let params = new HttpParams();
    if (query.status) params = params.set('status', query.status);
    if (query.assignment) params = params.set('assignment', query.assignment);
    return this.http.get<ClaimSummary[]>(`${this.base}/claims`, { params });
  }

  getClaim(id: string): Observable<ClaimDetail> {
    return this.http.get<ClaimDetail>(`${this.base}/claims/${id}`);
  }

  submitClaim(request: SubmitClaimRequest): Observable<ClaimDetail> {
    return this.http.post<ClaimDetail>(`${this.base}/claims`, request);
  }

  pickUp(claim: ClaimSummary): Observable<ClaimDetail> {
    return this.http.post<ClaimDetail>(`${this.base}/claims/${claim.id}/pick-up`, { version: claim.version });
  }

  setReserve(claim: ClaimSummary, amount: number): Observable<ClaimDetail> {
    return this.http.put<ClaimDetail>(`${this.base}/claims/${claim.id}/reserve`, { version: claim.version, amount });
  }

  requestInformation(claim: ClaimSummary, question: string): Observable<ClaimDetail> {
    return this.http.post<ClaimDetail>(`${this.base}/claims/${claim.id}/information-requests`, {
      version: claim.version,
      question,
    });
  }

  respond(claim: ClaimSummary, requestId: string, response: string): Observable<ClaimDetail> {
    return this.http.post<ClaimDetail>(
      `${this.base}/claims/${claim.id}/information-requests/${requestId}/response`,
      { version: claim.version, response },
    );
  }

  approve(claim: ClaimSummary, approvedAmount: number, reason: string): Observable<ClaimDetail> {
    return this.http.post<ClaimDetail>(`${this.base}/claims/${claim.id}/approve`, {
      version: claim.version,
      approvedAmount,
      reason,
    });
  }

  reject(claim: ClaimSummary, reason: string): Observable<ClaimDetail> {
    return this.http.post<ClaimDetail>(`${this.base}/claims/${claim.id}/reject`, { version: claim.version, reason });
  }

  settle(claim: ClaimSummary): Observable<ClaimDetail> {
    return this.http.post<ClaimDetail>(`${this.base}/claims/${claim.id}/settle`, { version: claim.version });
  }

  dashboard(): Observable<Dashboard> {
    return this.http.get<Dashboard>(`${this.base}/dashboard`);
  }

  notifications(): Observable<AppNotification[]> {
    return this.http.get<AppNotification[]>(`${this.base}/notifications`);
  }
}
