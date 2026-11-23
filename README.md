# Claims Platform — APAC Motor & Property Claims

A claims platform for motor and property claims across six APAC markets. **Claimants** report incidents,
track their claims, answer questions and receive decisions. **Claims officers** pick up and assess claims
and take them to settlement or rejection. **Managers** see team workload and outstanding liability exposure.
Everyone uses the same Angular app.

| Layer | Technology |
| :--- | :--- |
| Backend | ASP.NET Core (.NET 10), EF Core 10, SQLite |
| Messaging | Kafka (apache/kafka 3.9, KRaft) via Confluent.Kafka |
| Frontend | Angular 21 (standalone, signals, zoneless) |
| Tests | xUnit, SQLite in-memory |

---

## 1. Run It Locally

**Prerequisites:** .NET 10 SDK, Node 22+, Docker (for Kafka; optional, see below).

```bash
# 1. Kafka
docker compose up -d

# 2. API: http://localhost:5129 (Swagger at /swagger). Creates and seeds claims.db on first start.
dotnet run --project src/Claims.Api --launch-profile http

# 3. Frontend: http://localhost:4200 (proxies /api to the API)
cd frontend && npm install && npm start
```

**No Docker?** Run the API with Kafka switched off. Everything works except claimant notifications:
```bash
Kafka__Enabled=false dotnet run --project src/Claims.Api --launch-profile http
```

**Tests:** `dotnet test ClaimsManagement.slnx` (57 tests, no Docker needed).

**Reset demo data:** stop the API, delete `src/Claims.Api/claims.db*`, and start it again.

### Demo users

There's no real authentication (out of scope). Pick a user on the frontend's **Sign in as** page, or send the
id in the `X-User-Id` header (Swagger has an **Authorize** button for it).

| User | Role | Id |
| :--- | :--- | :--- |
| Jane Tan | Claimant | `10000000-0000-0000-0000-000000000001` |
| Arjun Kumar | Claimant | `10000000-0000-0000-0000-000000000002` |
| David Wong | Claims officer | `20000000-0000-0000-0000-000000000001` |
| Mei Lin | Claims officer | `20000000-0000-0000-0000-000000000002` |
| Sarah Lim | Manager | `30000000-0000-0000-0000-000000000001` |

Seven demo claims cover every status and five markets, so the queue and dashboard aren't empty.

### 2-minute walkthrough

1. **Jane** → *Report an incident* → submit.
2. **David** → *Queue (Unassigned)* → open the claim → *Pick up* → *Request information*.
3. **Jane** → open the claim → answer → *Updates* shows the notifications (delivered through Kafka).
4. **David** → *Approve* → *Settle*.
5. **Sarah** → *Dashboard*: workload, queue age, and liability exposure per market.
6. **Concurrency:** open one claim as David in two tabs, act in one, then act in the other. You'll get a 409, the claim reloads, and what you typed is kept.

---

## 2. Architecture

```
                 ┌──────────────── Angular app (one app, role-aware) ────────────────┐
                 │  claimant pages · officer pages · dashboard · updates (polling)   │
                 └───────────────────────────────┬───────────────────────────────────┘
                                                 │ REST/JSON  (X-User-Id, ProblemDetails errors)
┌────────────────────────────────── Claims.Api (modular monolith) ──────────────────────────────────┐
│ Controllers → ClaimService · ClaimWorkflowService · DashboardService                               │
│                     │ one transaction: claim + ClaimActivity (+ InformationRequest)                │
│                     ▼                                                                              │
│               SQLite (EF Core) ◀──────────────── ClaimNotificationHandler ◀─┐                       │
│                     │ after commit                                           │                      │
│                     ▼                                                        │                      │
│          KafkaClaimEventPublisher ──▶ [claim-events, key = claimId] ──▶ ClaimEventConsumer          │
└────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

### Key decisions

| Question from the brief | Decision |
| :--- | :--- |
| **One service or more?** | **One service, a modular monolith.** One team, one domain, and the lifecycle, audit and exposure all read the same tables, so separate services would only add distributed-transaction problems. Boundaries are kept in code (controllers, services, `Messaging/`), and the Kafka consumer only depends on Kafka and the DB, so it could move into its own worker unchanged. |
| **What is synchronous, what is event-driven?** | **REST** for everything a user waits on: submit, workflow actions (validation errors, 409s) and all reads. **Kafka** for side effects of a claim changing: one event per audit entry on `claim-events`. The first consumer creates claimant notifications. Email/SMS, reporting or fraud checks could subscribe without touching claim code. |
| **Async processing across both tiers?** | Backend: events are published only after commit. The consumer is at-least-once with an idempotent write (unique `EventId`). Frontend: every command sends the claim `version`. A 409 reloads the claim and keeps the user's input. The Updates page polls every 5s for notifications, which are eventually consistent. |
| **Frontend/backend contract, and who owns it?** | **The backend.** DTOs generate the OpenAPI document (`/openapi/v1.json`), and `frontend/src/app/core/models.ts` mirrors it. Errors are always RFC 7807 ProblemDetails. |
| **Core entities?** | See the data model below. |
| **What does an officer need to see?** | A queue (Unassigned / Mine / All plus a status filter), a claim page with the full timeline and open questions, and only the actions valid for the claim's state. The dashboard shows each officer's open claims, claims waiting on the claimant, decisions made, and average days to decision. |
| **What does the system need to know about exposure?** | Three amounts per claim: **claimed** (claimant), **reserve** (officer's estimate), **approved** (committed). Exposure of an open claim = `approved ?? reserve ?? claimed`. Rejected and settled claims are 0. It's totalled **per market/currency with no FX conversion**, and `unreservedClaims` shows how much of the total is still unassessed claimant figures. |

### Data model

```
AppUser (Claimant | ClaimsOfficer | Manager, IsActive soft-delete)
  ├─< Claim (as claimant)            ClaimNumber, Type, Market→Currency, Status,
  └─< Claim (as assigned officer)    Claimed / Reserve / Approved amounts, Decision, Version
          ├─< ClaimActivity          append-only audit trail (private setters), one per change
          ├─< InformationRequest     officer question → claimant answer (answer once)
          └─< Notification           created from Kafka events, unique EventId
```

Foreign keys use `Restrict` or `SetNull`, never cascade, so deleting a user or claim can't wipe audit history.

### Claim lifecycle

```
Submitted ──pick up──▶ UnderReview ──approve──▶ Approved ──settle──▶ Settled
                         │  ▲    └──reject──▶ Rejected
            request info │  │ claimant responds
                         ▼  │
               AdditionalInformationRequired ──reject──▶ Rejected
```

The lifecycle is defined in one transition table (`Models/ClaimWorkflow.cs`) that every action goes through.
Only the assigned officer can act on a claim. Every change appends a `ClaimActivity` in the same transaction.

### Concurrency

Each claim has a `Version`. The client sends the version it last saw. If it's stale, the API returns
**409** straight away. If two requests race between read and write, EF's `WHERE Version = @loaded` makes
the second one fail, which also gives **409**. Verified with parallel requests: exactly one wins, with no
duplicate audit entry.

---

## 3. API

| Method | Route | Who |
| :--- | :--- | :--- |
| GET | `/api/users`, `/api/users/me` | demo user list / current user |
| POST | `/api/claims` | claimant: report an incident |
| GET | `/api/claims?status=&assignment=All\|Mine\|Unassigned&marketCode=` | claimant: own claims · staff: queue |
| GET | `/api/claims/{id}` | owner or staff: detail, timeline, info requests |
| POST | `/api/claims/{id}/pick-up` | officer |
| PUT | `/api/claims/{id}/reserve` | assigned officer |
| POST | `/api/claims/{id}/information-requests` | assigned officer |
| POST | `/api/claims/{id}/information-requests/{requestId}/response` | owning claimant |
| POST | `/api/claims/{id}/approve` · `/reject` · `/settle` | assigned officer |
| GET | `/api/dashboard` | officers and managers |
| GET | `/api/notifications` | current user |

Errors: 400 validation · 401 no or unknown user · 403 wrong role or not assigned · 404 not found, or not
yours · 409 stale version or illegal transition. Ready-made requests are in `src/Claims.Api/Claims.Api.http`.

---

## 4. Prioritisation

The brief says the backend comes first and gets most of the time. The order was:

1. **Domain model and persistence:** entities, the audit trail, three amounts for exposure, concurrency token.
2. **Claims API:** submit, query, validation, consistent errors.
3. **Workflow:** the lifecycle rules and concurrency, the core of claims handling.
4. **Dashboard:** workload, performance and exposure, the managers' "real-time picture".
5. **Kafka:** the REST/Kafka split, with reliability handled properly (after-commit publish, idempotent consumer).
6. **Tests** on the rules that cost money if wrong, checked by deliberately breaking the code.
7. **Frontend** last: the full flow for all three roles, verified in a real browser.

Every step was verified by running it (scripted HTTP scenarios, a real Kafka broker, headless Chrome), not
only by compiling it. The details are in the per-step docs.

### Deliberately left out

| Left out | Why / what production would do |
| :--- | :--- |
| Real authentication | Timeboxed. `ICurrentUserService` is the single seam: swap the header for a validated JWT (OIDC). |
| Transactional outbox | Publish-after-commit can lose an event if the process dies right after commit. The claim stays correct, but a notification is missed. An outbox table plus relay fixes this. |
| Policies and coverage validation, document uploads | Large domains of their own. Not needed to show the claims lifecycle. |
| FX conversion | Without a rates source, a combined number would mislead, so totals are per currency. |
| Manager reassignment, teams per market, SLAs | Natural next features. Managers are read-only for now. |
| Paging, DB-side aggregation | Fine at this volume. At scale: DB aggregation, or a read model fed by `claim-events`. |
| HTTP-level and frontend tests | The business rules are covered by service tests. HTTP behaviour was checked with scripted end-to-end runs. |

---

## 5. Repository Guide

```
src/Claims.Api/
  Controllers/  Services/  Interfaces/  DTOs/  Errors/
  Models/        entities + ClaimWorkflow (transitions) + LiabilityExposure + Markets
  Messaging/     Kafka publisher, consumer, notification handler
  Data/          DbContext, migrations, seeder
tests/Claims.Api.Tests/   Domain/ (pure rules) · Services/ (real services + SQLite)
frontend/                 Angular app (see frontend/README.md)
docker-compose.yml        Kafka
docs/                     ROADMAP.md and one design note per step (task-03 … task-11)
AI_JOURNAL.md             what was asked of the AI, accepted, challenged, overridden
AGENTS.md                 working rules given to the AI agent
```

### Design notes per step

| Doc | Topic |
| :--- | :--- |
| [task-03](docs/task-03-domain-models.md) · [task-04a](docs/task-04a-efcore-setup.md) · [task-04b](docs/task-04b-initial-migration.md) · [task-05](docs/task-05-domain-extensions.md) | Domain model, EF Core, migrations, exposure fields |
| [task-06](docs/task-06-claims-api-core.md) | API core, identity, error contract |
| [task-07](docs/task-07-claim-workflow.md) | Lifecycle, check order, concurrency |
| [task-08](docs/task-08-team-dashboard.md) | Dashboard and exposure |
| [task-09](docs/task-09-kafka-events.md) | REST vs Kafka, reliability |
| [task-10](docs/task-10-tests.md) | Tests and the mutation check |
| [task-11](docs/task-11-angular-frontend.md) | Frontend decisions and bugs found in the browser |

### How AI was used

AI generated most of the code. I directed the scope step by step, reviewed and committed every step myself,
and required evidence before accepting claims: EF's model-drift check, throwaway programs to test framework
assumptions, real race conditions, a real Kafka broker, deliberate code-breaking for the tests, and a headless
browser for the UI. Several of the AI's assumptions and drafts were wrong and were corrected.
[AI_JOURNAL.md](AI_JOURNAL.md) records each case.
