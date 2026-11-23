# Task 08 — Team Dashboard & Liability Exposure

Answers the brief's "see their team's workload and performance" (claims staff) and
"real-time picture of outstanding claims or liability exposure" (managers).

---

## 1. Endpoint

`GET /api/dashboard`: claims officers and managers. Claimants get 403, no header gets 401.

| Section | Content | Question it answers |
| :--- | :--- | :--- |
| `claimsByStatus` | Count for every status, zeros included | Where is the work piling up? |
| `queue` | Unassigned submitted claims, and the age in days of the oldest | Is incoming work being picked up? |
| `officers[]` | Per active officer: `openClaims`, `awaitingClaimant`, `approved`, `rejected`, `averageDaysToDecision` | Who is overloaded, who is waiting on claimants, how fast are decisions made? |
| `exposure[]` | Per market and currency: `openClaims`, `totalClaimed`, `outstandingExposure`, `unreservedClaims` | What do we still owe, and how reliable is that figure? |

It is computed at request time, so it reflects the current state of every claim.

## 2. Liability Exposure

```
exposure(claim) = 0                                        if Rejected or Settled
                = ApprovedAmount ?? ReserveAmount ?? ClaimedAmount   otherwise
```

The formula is in `Models/LiabilityExposure.cs`, and the open statuses come from `ClaimWorkflow.OpenStatuses`.
Both are shared with the workflow, so "open" means the same thing everywhere.

- **Grouped by market.** Each market has exactly one currency, so per-market totals never mix currencies.
  There's no FX conversion; see Task 05.
- **`totalClaimed` next to `outstandingExposure`** shows the gap between what claimants asked for and
  the insurer's best current estimate.
- **`unreservedClaims`** flags exposure that still depends on the claimant's own figure because no
  officer has assessed it yet. A manager can see how much of the number is unreviewed.

## 3. Definitions

- **Decided** = `DecidedAt` is set. **Approved** counts Approved + Settled (a settled claim was approved first).
- **Average days to decision** = mean of `DecidedAt − CreatedAt`, rounded to 0.1. Null if no decisions yet.
- **Team** = all active claims officers. Teams per market are out of scope.

## 4. Design Decisions

1. **Aggregated in memory, in a pure `DashboardService.Build(claims, officers, now)`.** It can be
   unit-tested without a database (Step 10), and it works around SQLite's weak `DateTimeOffset` support.
   - *Corrected during this step:* I first assumed SQLite couldn't `SUM` decimals. A throwaway test on
     EF Core 10.0.15 showed it can, so that is **not** used as a reason.
   - *Trade-off:* every claim is loaded per request. That's fine for this data set. At production scale
     it would become database-side aggregation, or a read model kept up to date by the Kafka claim events from Step 09.
2. **Demo claims are seeded** (7 claims covering every status, 5 markets, both officers), so the queue
   and dashboard have data straight after `dotnet run`. They are seeded only when the Claims table is empty.
3. **Backdated seed timelines.** `ClaimActivity.CreatedAt` is a private setter, so application code
   can't backdate audit entries. The seeder alone sets it explicitly through EF's change tracker, and
   the domain API stays strict. Seeded `Version` equals the number of changes after submission, the same
   as the workflow would produce, so workflow actions work on seeded claims.

## 5. Verification (fresh database)

Expected values were calculated by hand from the seed data before running:

| Check | Expected | Actual |
| :--- | :--- | :--- |
| Status counts | 2 / 1 / 1 / 1 / 1 / 1 | ✅ |
| Queue | 2 unassigned, oldest 3 days | ✅ |
| David | open 2, approved 1, avg 8 days | ✅ |
| Mei | open 1, awaiting 1, approved 1, rejected 1, avg 6 days (7 and 5) | ✅ |
| AU | claimed 9000, exposure 8500 (approved amount) | ✅ |
| HK | claimed 80000, exposure 65000 (reserve) | ✅ |
| MY / SG | exposure equals claimed, all unreserved | ✅ |
| TH (rejected) | absent | ✅ |
| Claimant / no header / officer | 403 / 401 / 200 | ✅ |
| Settle seeded AU claim with `version: 2` | 200; AU leaves exposure; David open 2 → 1 | ✅ |
| Server log | 0 unhandled errors | ✅ |
