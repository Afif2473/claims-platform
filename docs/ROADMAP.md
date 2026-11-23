# Roadmap — Claims Platform

Time budget: 2–3 h target, 5 h hard cap. The brief says the backend comes first and gets most of the time.
A solid backend with a partial frontend beats a rushed attempt at both.

## Requirements traced from the brief

| Brief requirement | Covered by |
| :--- | :--- |
| Claimant: report an incident | Step 06 (`POST /api/claims`) |
| Claimant: track their claim | Step 06 (`GET /api/claims/mine`, detail and timeline) |
| Claimant: provide info when asked | Step 07 (respond to an information request) |
| Claimant: receive decisions | Step 07 (decision on claim) and Step 09 (Kafka → notification) |
| Staff: pick up incoming claims | Step 06 (unassigned queue) and Step 07 (pick up) |
| Staff: review/assess, settle or reject | Step 07 (state machine) |
| Staff: team workload and performance | Step 08 (dashboard) |
| Managers: outstanding claims and liability exposure | Step 08 (exposure per currency) |
| REST **and** Kafka, with a reasoned split | Steps 06–09 and README |
| One Angular app for both user types | Step 11 |
| Git history, runs locally, AI journal, docs | Every step, plus Step 12 |

## Status

| # | Step | Status |
| :--- | :--- | :--- |
| 01 | Environment, repo, `.gitignore` | ✅ Done |
| 02 | ASP.NET Core API skeleton, Swagger, health check | ✅ Done |
| 03 | Domain models and enums, append-only `ClaimActivity` | ✅ Done |
| 04 | EF Core and SQLite, Fluent API, `InitialCreate` migration | ✅ Done |
| 05 | Reserve/approved amounts, decisions, `InformationRequest`, `Version` concurrency, `IsActive` | ✅ Done (model code restored to match the migration, see journal 05B) |
| 06 | **Claims API core**: DTOs and validation, `ClaimService`, submit / list / detail, seeded users, simulated identity (`X-User-Id`), error handling (ProblemDetails, 409 on concurrency conflict) | ✅ Done (see `docs/task-06-claims-api-core.md`) |
| 07 | **Claim workflow**: pick up, request info, respond, approve (with amount), reject, settle; one transition table that enforces the lifecycle; every change appends a `ClaimActivity` | ✅ Done (see `docs/task-07-claim-workflow.md`) |
| 08 | **Team dashboard**: open claims by status, workload per officer, performance (decided count, average days to decision), outstanding exposure per currency | ✅ Done (see `docs/task-08-team-dashboard.md`) |
| 09 | **Kafka**: publish `claim-events` after state changes, consume to create claimant notifications; `docker-compose.yml`; `Kafka:Enabled` switch so the app still runs without Docker | ✅ Done (see `docs/task-09-kafka-events.md`) |
| 10 | **Tests**: xUnit on the transition rules and the exposure calculation | ✅ Done, 57 tests (see `docs/task-10-tests.md`) |
| 11 | **Angular frontend (partial)**: user switcher, claimant screens (submit, my claims, detail, respond), officer screens (queue, claim actions, dashboard) | ✅ Done (see `docs/task-11-angular-frontend.md`) |
| 12 | **README and wrap-up**: run instructions, architecture and REST/Kafka rationale, what was left out and why, journal finalised | ✅ Done (see root `README.md`) |

## Key decisions (planned)

- **One service (modular monolith).** A single team building a single domain doesn't justify splitting
  into separate services. Kafka still decouples side effects such as notifications from the request path.
- **REST for commands and queries.** The user needs an immediate answer: validation errors, the new
  claim number, a 409 when someone else already picked up the claim.
- **Kafka for domain events.** Things that happen *because* a claim changed, such as notifying the
  claimant, don't need to block the officer's request and can be retried on their own.
- **Contract is defined by the backend.** DTOs drive the OpenAPI document at `/openapi/v1.json`, and the
  Angular models mirror it.
- **Identity is simulated.** Seeded users and an `X-User-Id` header, with roles checked in the service.
  Real auth (OIDC/JWT) is out of scope for the timebox.

## Deliberately out of scope

Real authentication, policy and coverage validation, document uploads, FX conversion, email/SMS delivery,
multi-service deployment, outbox pattern (noted as the production fix for publish-after-commit).

## Environment notes

- Docker Desktop WSL integration is enabled. Kafka runs with `docker compose up -d`.
  Without Docker, run with `Kafka__Enabled=false`.
- Node 22 is installed. The frontend uses a project-local Angular CLI (`npm start` in `frontend/`).
