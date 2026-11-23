// TypeScript mirror of the API contract. The backend DTOs are the source of truth;
// see /openapi/v1.json. Enums travel as strings.

export type UserRole = 'Claimant' | 'ClaimsOfficer' | 'Manager';
export type ClaimType = 'Motor' | 'Property';
export type ClaimStatus =
  | 'Submitted'
  | 'UnderReview'
  | 'AdditionalInformationRequired'
  | 'Approved'
  | 'Rejected'
  | 'Settled';

export const MARKETS = ['SG', 'MY', 'HK', 'TH', 'ID', 'AU'] as const;

export const STATUS_LABELS: Record<ClaimStatus, string> = {
  Submitted: 'Submitted',
  UnderReview: 'Under review',
  AdditionalInformationRequired: 'Information needed',
  Approved: 'Approved',
  Rejected: 'Rejected',
  Settled: 'Settled',
};

export interface User {
  id: string;
  fullName: string;
  email: string;
  role: UserRole;
}

export interface ClaimSummary {
  id: string;
  claimNumber: string;
  claimType: ClaimType;
  status: ClaimStatus;
  marketCode: string;
  currencyCode: string;
  claimedAmount: number;
  reserveAmount: number | null;
  approvedAmount: number | null;
  incidentDate: string;
  claimantId: string;
  claimantName: string;
  assignedOfficerId: string | null;
  assignedOfficerName: string | null;
  createdAt: string;
  updatedAt: string;
  version: number;
}

export interface ClaimActivity {
  id: string;
  activityType: string;
  description: string;
  actorName: string | null;
  createdAt: string;
}

export interface InformationRequest {
  id: string;
  question: string;
  response: string | null;
  requestedByName: string;
  createdAt: string;
  respondedAt: string | null;
  isAnswered: boolean;
}

export interface ClaimDetail extends ClaimSummary {
  description: string;
  incidentLocation: string;
  decisionReason: string | null;
  decidedAt: string | null;
  activities: ClaimActivity[];
  informationRequests: InformationRequest[];
}

export interface SubmitClaimRequest {
  claimType: ClaimType;
  description: string;
  incidentLocation: string;
  incidentDate: string;
  claimedAmount: number;
  marketCode: string;
}

export type AssignmentFilter = 'All' | 'Mine' | 'Unassigned';

export interface ClaimListQuery {
  status?: ClaimStatus | '';
  assignment?: AssignmentFilter;
}

export interface Dashboard {
  generatedAt: string;
  claimsByStatus: { status: ClaimStatus; count: number }[];
  queue: { unassigned: number; oldestUnassignedDays: number | null };
  officers: {
    officerId: string;
    officerName: string;
    openClaims: number;
    awaitingClaimant: number;
    approved: number;
    rejected: number;
    averageDaysToDecision: number | null;
  }[];
  exposure: {
    marketCode: string;
    currencyCode: string;
    openClaims: number;
    totalClaimed: number;
    outstandingExposure: number;
    unreservedClaims: number;
  }[];
}

export interface AppNotification {
  id: string;
  claimId: string;
  message: string;
  createdAt: string;
}
