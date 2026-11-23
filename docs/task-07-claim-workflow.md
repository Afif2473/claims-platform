# Task 07 — Claim Workflow

Officers can now take a claim from submission to settlement or rejection. Claimants can answer
information requests and see the decision.

---

## 1. Lifecycle

```
Submitted ──pick up──▶ UnderReview ──approve──▶ Approved ──settle──▶ Settled
                         │  ▲    └──reject──▶ Rejected
            request info │  │ claimant responds
                         ▼  │
               AdditionalInformationRequired ──reject──▶ Rejected
```

The allowed transitions are defined in **one table**, `Models/ClaimWorkflow.cs`. Every action calls
`MoveTo(claim, target)`, which checks that table, so no action can bypass the lifecycle.
`ClaimWorkflow.OpenStatuses` (Submitted, UnderReview, AdditionalInformationRequired, Approved) is also
defined there and is reused by the exposure calculation in Step 08.

## 2. Endpoints

All take a JSON body containing `version`, and all return the updated claim detail.

| Method | Route | Who | Transition | Extra rules |
| :--- | :--- | :--- | :--- | :--- |
| `POST` | `/api/claims/{id}/pick-up` | Any officer | Submitted → UnderReview | Assigns the officer |
| `PUT` | `/api/claims/{id}/reserve` | Assigned officer | none | Only while UnderReview / AdditionalInformationRequired |
| `POST` | `/api/claims/{id}/information-requests` | Assigned officer | UnderReview → AdditionalInformationRequired | Creates an `InformationRequest` |
| `POST` | `/api/claims/{id}/information-requests/{requestId}/response` | Owning claimant | AdditionalInformationRequired → UnderReview | Answer once only |
| `POST` | `/api/claims/{id}/approve` | Assigned officer | UnderReview → Approved | `approvedAmount ≤ claimedAmount`; reason required |
| `POST` | `/api/claims/{id}/reject` | Assigned officer | UnderReview / AdditionalInformationRequired → Rejected | Reason required |
| `POST` | `/api/claims/{id}/settle` | Assigned officer | Approved → Settled | |

## 3. Check Order

Every action runs the same checks in the same order, so errors are predictable:

| # | Check | Failure |
| :--- | :--- | :--- |
| 1 | Body is valid (`version` present, amounts 2dp, lengths) | 400 |
| 2 | Caller has the right role | 403 |
| 3 | Claim exists and is visible to the caller | 404 |
| 4 | Caller is the assigned officer, or the owning claimant | 403 (404 for another claimant) |
| 5 | `version` matches the current claim | 409 Concurrency conflict |
| 6 | The transition is legal | 409 Business rule violation |
| 7 | Action-specific rule (amount ≤ claimed, not already answered) | 409 Business rule violation |

Then the change, its `ClaimActivity` and any `InformationRequest` are written in **one `SaveChanges`**
(one transaction). `Version` is incremented and `UpdatedAt` is set.

## 4. Concurrency — Two Layers

1. **Stale screen.** The client sends the `version` it last saw. If the claim has changed since, the
   service returns 409 before doing anything. This covers an officer acting on a page that has been
   open for ten minutes.
2. **Simultaneous requests.** Both requests read the same version and both pass layer 1. EF issues
   `UPDATE … WHERE Id = @id AND Version = @loaded`. The second UPDATE matches no row, EF throws
   `DbUpdateConcurrencyException`, and that maps to 409.

Checked with two parallel pick-up requests on the same claim, 3 runs. Each time exactly one 200 and
one 409, and the claim has exactly 2 activities, so no duplicate "picked up" audit entry.

## 5. Design Decisions

- **Managers are read-only.** Only claims officers work claims. Managers get the dashboard in Step 08.
  Reassignment by a manager is a natural next feature but is out of scope.
- **Only one open information request at a time.** Requesting is only allowed from UnderReview, so the
  claim cannot get stuck with several half-answered questions. Answering moves it straight back to UnderReview.
- **Rejecting from AdditionalInformationRequired is allowed**, for a claimant who never responds. A late
  answer is then refused with 409 ("not waiting for information").
- **Approved amount cannot exceed claimed.** Approving less (for example after an excess is deducted) is normal.
- **Activity descriptions are short and fixed-format.** The question text and the decision reason are
  stored on their own records, which keeps activity descriptions within the 1000-character limit.
- **New `ClaimActivityType.ReserveSet`.** Enums are stored as strings, so no migration was needed. The
  drift check confirmed this.

## 6. Verification (end to end, scratch database)

| Scenario | Result |
| :--- | :--- |
| Approve before pick-up | 403 not assigned |
| Manager picks up | 403 |
| Pick up without `version` | 400 |
| David picks up v0 | 200 UnderReview v1 |
| Mei picks up with stale v0 | 409 concurrency |
| Mei picks up with v1 | 409 UnderReview → UnderReview illegal |
| Mei sets reserve | 403 not assigned |
| Reserve, then request info | 200 v2, then 200 AdditionalInformationRequired v3 |
| Request info again / approve while waiting | 409 illegal transition |
| Arjun responds to Jane's request | 404 |
| Officer responds | 403 |
| Jane responds, then responds again | 200 UnderReview v4, then 409 already answered |
| Approve 6000 on a 5000 claim / approve 1.234 | 409 / 400 |
| Settle before approval | 409 |
| Approve 4200, reject after approval, settle | 200, 409, 200 Settled v6 |
| Reserve on a settled claim | 409 |
| Reject while waiting for info, then late response | 200 Rejected, then 409 |
| Parallel pick-up race (3 runs) | one 200 and one 409 each time, 2 activities |
| Server log | 0 unhandled errors |
