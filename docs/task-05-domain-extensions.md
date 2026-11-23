# Task 05 — Domain Extensions: Liability, Decisions & Information Requests

Extends the Task 03 model so it can support liability exposure, claim decisions, the
"provide additional information" flow, and safe concurrent updates.

Migration: `20261121122708_AddLiabilityAndInformationRequests`

---

## 1. Changes

### `Claim`
| Property | Type | Purpose |
| :--- | :--- | :--- |
| `ReserveAmount` | `decimal?` | Officer's estimate of the eventual cost, set during assessment. |
| `ApprovedAmount` | `decimal?` | Amount committed to pay, set on approval. |
| `DecisionReason` | `string?` (1000) | Why the claim was approved or rejected. Shown to the claimant. |
| `DecidedAt` | `DateTimeOffset?` | When the decision was recorded. |
| `Version` | `int` (concurrency token) | Optimistic concurrency. |
| `InformationRequests` | navigation | Open and answered questions on this claim. |

### `AppUser`
| Property | Type | Purpose |
| :--- | :--- | :--- |
| `IsActive` | `bool` (default `true` in C#) | Soft delete. Users are deactivated instead of deleted, so audit attribution survives. |

### New entity: `InformationRequest`
| Property | Notes |
| :--- | :--- |
| `ClaimId` → `Claim` | Required, `Restrict` on delete. |
| `RequestedByUserId` → `AppUser` | Required, `Restrict` on delete (users are soft-deleted anyway). |
| `Question` (1000), `Response` (2000, nullable) | |
| `CreatedAt`, `RespondedAt` | `IsAnswered` is derived from `RespondedAt` and not stored. |

Properties use `private set`, and the only way to answer is `Respond(response)`. It throws if the request
is already answered, so the "answer once" rule lives on the entity and not in every caller.

---

## 2. Liability Exposure Formula

```
exposure(claim) = ApprovedAmount ?? ReserveAmount ?? ClaimedAmount
outstanding exposure = Σ exposure(claim) for open claims, grouped by CurrencyCode
open = Submitted | UnderReview | AdditionalInformationRequired | Approved
```

- **Three amounts, not one.** The claimed figure is the claimant's ask. The reserve is the officer's
  estimate. The approved figure is a commitment. Using only `ClaimedAmount` would overstate liability.
- **Approved but not settled counts as exposure.** Money is committed but has not been paid out yet.
- **Rejected and Settled claims are excluded.** They carry no outstanding liability.
- **Per-currency totals, no FX conversion.** Adding SGD to AUD without a rates source gives a misleading
  number. Six markets means at most a handful of currency rows.

---

## 3. Design Decisions

1. **`int Version` instead of `[Timestamp] byte[] RowVersion`.** SQLite has no auto-generated
   rowversion, so the service increments `Version` on every update. EF includes
   `WHERE Version = @original` in the UPDATE. A stale write throws `DbUpdateConcurrencyException`,
   which the API maps to **HTTP 409 Conflict**. Example: two officers try to pick up the same claim at once.
2. **Information requests get their own table.** `ClaimActivity` is append-only history. An info request
   is a work item with open/closed state. Creating or answering one also appends a `ClaimActivity`, so
   the timeline stays complete.
3. **No DB default for `IsActive` or `Version`.** EF Core treats the CLR default (`false` / `0`) as
   "not set" for a property with a database default, and omits it from the INSERT. An explicit
   `IsActive = false` would then be silently stored as `true`. The C# initializer is the single source
   of truth. The migration still uses `defaultValue: true` for the `AddColumn` backfill, so existing
   rows stay active.

---

## 4. Out of Scope (deliberate)

- Policy entity and coverage validation
- Document and photo uploads
- Decision history beyond the audit log (a re-decision overwrites `DecisionReason`, and the earlier one stays in `ClaimActivities`)

---

## 5. Verification

```bash
dotnet build ClaimsManagement.slnx
# Build succeeded. 0 Warning(s), 0 Error(s)

dotnet tool run dotnet-ef migrations has-pending-model-changes --project src/Claims.Api/Claims.Api.csproj
# No changes have been made to the model since the last migration.

dotnet tool run dotnet-ef database update --project src/Claims.Api/Claims.Api.csproj
# Done.
```
