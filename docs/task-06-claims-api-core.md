# Task 06 — Claims API Core

First working slice of the API. A claimant can report an incident and track it. Staff can see
the incoming queue and open any claim.

---

## 1. Endpoints

| Method | Route | Who | Result |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/users` | anyone (demo only) | Seeded users, for the frontend "sign in as" switcher |
| `GET` | `/api/users/me` | any user | The current user |
| `POST` | `/api/claims` | Claimant | `201` with the claim detail and a `Location` header |
| `GET` | `/api/claims` | Claimant: own claims, newest first. Staff: all claims, oldest first (work queue) | Summary list |
| `GET` | `/api/claims/{id}` | Claimant: own claim only. Staff: any claim | Detail with timeline and information requests |

Staff filters on `GET /api/claims`: `status`, `assignment=All|Mine|Unassigned`, `marketCode`.

## 2. Simulated Identity

Callers send `X-User-Id: <seeded user id>`. `CurrentUserService` loads the **active** user. If the
header is missing or doesn't match an active user, it throws, and the API returns **401**.

- Role checks are in the service, not the controller, so the business rules stay in one place.
- **This is not security.** Anyone can send any id. It stands in for a validated JWT. Services depend
  on `ICurrentUserService`, so moving to real auth only changes that one class.
- Swagger UI has an **Authorize** button that sets the header for every request.

Seeded users (created on first start):

| Id | Name | Role |
| :--- | :--- | :--- |
| `10000000-0000-0000-0000-000000000001` | Jane Tan | Claimant |
| `10000000-0000-0000-0000-000000000002` | Arjun Kumar | Claimant |
| `20000000-0000-0000-0000-000000000001` | David Wong | ClaimsOfficer |
| `20000000-0000-0000-0000-000000000002` | Mei Lin | ClaimsOfficer |
| `30000000-0000-0000-0000-000000000001` | Sarah Lim | Manager |

## 3. Error Contract

Every error is RFC 7807 `ProblemDetails`. Services throw domain exceptions and `ApiExceptionHandler`
maps them to HTTP statuses.

| Situation | Status |
| :--- | :--- |
| Invalid payload (attributes, then cross-field rules) | 400 (`errors` keyed by field) |
| Missing, invalid or unknown `X-User-Id` | 401 |
| Wrong role for the action | 403 |
| Claim doesn't exist, **or belongs to another claimant** | 404 (doesn't reveal which ids exist) |
| Business rule or state conflict (Step 07) | 409 |
| `DbUpdateConcurrencyException` (stale `Version`) | 409 |
| Anything else | 500 with a generic message; details are logged, never returned |

## 4. Design Decisions

1. **Currency comes from the market.** The claimant picks a market (`SG, MY, HK, TH, ID, AU`) and the
   currency is looked up from it. This makes a mismatch like "SG claim in THB" impossible. It replaces
   the Task 03 plan of validating a separately submitted currency code.
2. **Validation happens in two passes.** DataAnnotations check shape and length. Then
   `IValidatableObject` checks the market list, that the incident date isn't in the future, and at most
   2 decimal places. SQLite doesn't enforce decimal scale, so the code has to. ASP.NET only runs the
   second pass once the first has passed.
3. **Claim number is `CLM-{market}-{year}-{8 hex}`.** A sequential number would need
   `SELECT MAX + 1`, which can race under concurrent submissions. The unique index is the final guard.
4. **Submit writes the claim and its `ClaimSubmitted` activity in one `SaveChanges`**, which is one
   transaction. No claim can exist without its first audit entry.
5. **Lists are sorted in memory.** EF Core 10's SQLite provider throws `NotSupportedException` for
   `ORDER BY` on `DateTimeOffset` (checked with a throwaway console app). That's fine at this data volume.
   With PostgreSQL or SQL Server the sort would move back into the query and paging would be added.
6. **Migrate and seed on startup.** `dotnet run` is the only step needed to get a working local database.

## 5. Out of Scope (deliberate)

Paging, real authentication, and the workflow actions (Step 07).

## 6. Verification

Ran against a throwaway SQLite database on a separate port:

| Check | Result |
| :--- | :--- |
| Build | 0 warnings, 0 errors |
| `GET /api/users` | 5 seeded users |
| No header | 401 ProblemDetails |
| Unknown user id | 401 |
| Jane submits a claim with `marketCode: "sg"` | 201, `CLM-SG-2026-…`, `SGD`, `Submitted`, `Version 0`, one `ClaimSubmitted` activity |
| Missing fields / too short | 400 with field errors |
| Future date, 3 decimal places, market `US` | 400 with all three cross-field errors |
| Unknown enum `"Boat"` | 400 |
| Officer submits | 403 |
| Arjun opens Jane's claim | 404 |
| Officer opens Jane's claim | 200 |
| Arjun's list | `[]` |
| Officer queue `?assignment=Unassigned&status=Submitted&marketCode=SG` | Jane's claim |
| `/openapi/v1.json` | `X-User-Id` security scheme present |
