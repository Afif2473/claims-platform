# AI Working Journal — Claims Management System

This journal logs the prompts, design discussions, AI suggestions accepted, challenged, or overridden, and the reasoning behind each decision throughout the development process.

---

## Entry 01 — Environment Setup & Baseline (Task 01)
- **Prompt**: Inspect environment (WSL 2, .NET SDK, Git, working directory) without modifying settings or installing packages.
- **Accepted**: Verified Ubuntu 24.04 on WSL 2, .NET 10 SDK, Git 2.43.0. Established baseline `.gitignore` covering .NET build outputs (`bin/`, `obj/`), SQLite databases (`*.db*`), and Node/Angular artifacts.
- **Action**: Initialized repository and set committer identity.

---

## Entry 02 — Backend Foundation & Solution Setup (Task 02)
- **Prompt**: Create `ClaimsManagement` solution and `Claims.Api` project targeting .NET 10 with controllers, Swagger UI, and modular monolith folder structure.
- **Accepted**:
  - Leveraged .NET 10's native `.slnx` solution format.
  - Used controller-based architecture for clean separation of concerns.
  - Integrated `Swashbuckle.AspNetCore.SwaggerUI` alongside .NET 10's built-in `Microsoft.AspNetCore.OpenApi`.
  - Added clean baseline `HealthController` and verified HTTP 200 via `curl`.
- **Reasoning**: Starting with an empty, verified skeleton avoids framework noise and establishes clear architectural boundaries early.

---

## Entry 03 — Domain Models & Review Feedback (Task 03)
- **Prompt**: Create initial domain models (`AppUser`, `Claim`, `ClaimActivity`) and enums (`UserRole`, `ClaimType`, `ClaimStatus`, `ClaimActivityType`) using C# conventions, `Guid` IDs, `decimal` for money, and `DateTimeOffset` for timestamps.
- **Accepted**:
  - Created models with navigation properties and enum types representing APAC motor/property claim lifecycles.
  - Avoided premature database or authentication complexity.

### Critical Feedback & Overrides by Developer:
1. **Audit Immutability Challenge**:
   - *Observation*: The AI initially described `ClaimActivity` as immutable, but had public `{ get; set; }` properties, meaning any service could mutate historical audit records.
   - *Resolution*: Enforced `private set;` on `ClaimActivity` properties, added a constructor requiring mandatory audit fields, and provided a protected parameterless constructor for EF Core.
2. **Entity Framework Multiple Relationship Disambiguation**:
   - *Observation*: `Claim` has two separate relationships to `AppUser` (`Claimant` and `AssignedOfficer`).
   - *Resolution*: Agreed to handle explicit relationship mapping via EF Core Fluent API in the database configuration layer (Task 04) rather than polluting domain entity classes with attributes.
3. **Validation Placement & Separation of Concerns**:
   - *Observation*: Entities should not be overburdened with every business rule (e.g. positive amounts, ISO currency codes, valid market whitelist).
   - *Resolution*: Decided that request validation belongs in DTOs (incoming payload sanity) and business rules belong in the Service layer, keeping domain entities clean representations of persisted state.
4. **Documentation Correction (Application-Level Protection)**:
   - *Observation*: Clarify in docs that `ClaimActivity` provides application-level protection against accidental modification by ordinary code, rather than claiming absolute immutability or permanence.
   - *Resolution*: Updated `docs/task-03-domain-models.md` with accurate phrasing ("application-level append-only audit log", "historical record").

---

## Entry 04A — EF Core & SQLite Database Configuration (Task 04A)
- **Prompt**: Introduce Entity Framework Core with SQLite. Install `Microsoft.EntityFrameworkCore.Sqlite` and `Microsoft.EntityFrameworkCore.Design` (compatible 10.x versions). Create `ClaimsDbContext` configuring explicit relationships, constraints, indexes, and deletion behaviors via Fluent API. Register in `Program.cs` with `appsettings.json` connection string. Do not create migrations yet.
- **Actions & Decisions**:
  1. *Package Installation & Compatibility*:
     - Attempted default package install; NuGet attempted to pull `11.0.0` which failed compatibility against `net10.0`.
     - Specified exact 10.0 matching versions (`10.0.15`), aligning with .NET 10 SDK and ASP.NET Core OpenApi packages.
     - Checked for `dotnet-ef`; confirmed it is not yet installed globally or locally.
  2. *Explicit Relationship Mapping*:
     - Mapped `Claim.ClaimantId -> AppUser.SubmittedClaims` as required with `DeleteBehavior.Restrict`.
     - Mapped `Claim.AssignedOfficerId -> AppUser.AssignedClaims` as optional with `DeleteBehavior.SetNull`.
     - Mapped `ClaimActivity.ClaimId -> Claim.Activities` as required with `DeleteBehavior.Restrict`.
     - Mapped `ClaimActivity.ActorUserId -> AppUser` as optional with `DeleteBehavior.SetNull`.
  3. *Deletion Behavior Rationale*:
     - Deleting a claimant user does not delete their claims (`Restrict`), protecting insurance liability records.
     - Deleting an officer unassigns the claim (`SetNull`), keeping the claim intact for re-assignment.
     - Deleting a claim cannot cascade delete activities (`Restrict`), protecting audit history.
     - Deleting an actor user does not erase the audit activity (`SetNull`), maintaining historical auditability.
  4. *Indexes & Constraints*:
     - Added unique indexes on `Claim.ClaimNumber` and `AppUser.Email`.
     - Added query-optimizing indexes on `Claim.Status`, `Claim.MarketCode`, `Claim.AssignedOfficerId`, and `CreatedAt`.
     - Enforced string maximum lengths and decimal precision (`18, 2`).
     - Stored enums as strings for human-readable database transparency and resilience.
  5. *Build Verification*:
     - Verified `dotnet build ClaimsManagement.slnx` succeeded with 0 warnings and 0 errors.

### Critical Feedback & Refinements by Developer (Task 04A):
1. **Package Version Pinning**:
   - *Observation*: Packages were referenced with a floating wildcard (`10.0.*`), risking non-deterministic restore behavior across environments.
   - *Resolution*: Pinned `Microsoft.EntityFrameworkCore.Sqlite` and `Microsoft.EntityFrameworkCore.Design` to exact version `10.0.15` in `Claims.Api.csproj`, guaranteeing reproducible builds.
2. **SQLite Decimal Precision Clarification**:
   - *Observation*: Stating that `HasPrecision(18, 2)` strictly enforces precision overlooks SQLite's dynamic type affinity.
   - *Resolution*: Clarified in documentation that `HasPrecision(18, 2)` documents model intent and schema metadata, but SQLite does not enforce decimal scale like SQL Server or PostgreSQL. Monetary precision, rounding, and validation must be actively enforced in application code (DTOs and service logic).
3. **Deletion vs. Production Retention Realities**:
   - *Observation*: `Restrict` and `SetNull` protect against accidental ORM cascade-deletions, but do not by themselves guarantee regulatory compliance or tamper-proofing.
   - *Resolution*: Updated documentation to reflect that production insurance platforms avoid hard-deleting user accounts, favoring user deactivation (`IsActive = false` / soft delete) so historical actor attribution remains intact.

---

## Entry 04B — Initial EF Core Migration & Database Creation (Task 04B)
- **Prompt**: Create and apply the initial EF Core database migration (`InitialCreate`). Set up `dotnet-ef` 10.0.15 via local tool manifest, inspect generated migration before applying, update the SQLite database, verify `claims.db` creation and `.gitignore` status, and produce documentation.
- **Commands Used**:
  - `dotnet new tool-manifest`
  - `dotnet tool install dotnet-ef --version 10.0.15`
  - `dotnet tool run dotnet-ef migrations add InitialCreate --project src/Claims.Api/Claims.Api.csproj --startup-project src/Claims.Api/Claims.Api.csproj --output-dir Data/Migrations`
  - `dotnet tool run dotnet-ef database update --project src/Claims.Api/Claims.Api.csproj --startup-project src/Claims.Api/Claims.Api.csproj`
  - `dotnet tool run dotnet-ef migrations list --project src/Claims.Api/Claims.Api.csproj --startup-project src/Claims.Api/Claims.Api.csproj --no-build`
  - `dotnet build ClaimsManagement.slnx`
- **Actions & Inspection**:
  1. *Tool Manifest Setup*: Verified absence of `.config/dotnet-tools.json`, created manifest with `dotnet new tool-manifest`, and installed `dotnet-ef` pinned to version `10.0.15`.
  2. *Pre-Migration Inspection*: Inspected `20261120085439_InitialCreate.cs`. Verified tables `Users`, `Claims`, and `ClaimActivities`; confirmed foreign keys (`Restrict` for Claimant and ClaimActivity, `SetNull` for AssignedOfficer and ActorUser); confirmed unique indexes on `Email` and `ClaimNumber`; confirmed string enum column types.
  3. *Database Application*: Ran `database update`. SQLite database created at `src/Claims.Api/claims.db`.
  4. *Git & Ignored Files*: Confirmed `claims.db` is ignored by Git and not staged.
  5. *Verification Results*: Migration `20261120085439_InitialCreate` recognized in `__EFMigrationsHistory`. Build succeeded with 0 warnings and 0 errors.
  6. *Live Smoke Test*: Started API, verified `GET /api/health` returned HTTP 200 with `"database": "Connected"` (confirming EF Core SQLite connectivity over HTTP), shut down cleanly, and verified database persistence and migration state.

---

## Entry 05 — Domain Extensions: Liability, Decisions & Information Requests (Task 05)
- **Prompt**: Review progress against the brief and outline remaining work. Then extend the domain model so it can support liability exposure, decisions, the info-request flow and concurrency safety. Explain the plan before implementing.
- **AI Proposal (accepted)**:
  - `Claim`: `ReserveAmount`, `ApprovedAmount`, `DecisionReason`, `DecidedAt`, and an `int Version` concurrency token.
  - New `InformationRequest` entity (separate table, encapsulated `Respond()` method, derived `IsAnswered`).
  - `AppUser.IsActive` soft-delete flag, turning the 04A documentation note into code.
  - Exposure formula: open claims only, `ApprovedAmount ?? ReserveAmount ?? ClaimedAmount`, aggregated **per currency** with no FX conversion.
- **Design Decisions & Reasoning**:
  1. *Three distinct amounts*: claimed (claimant), reserve (officer estimate), approved (committed). Exposure based only on `ClaimedAmount` would overstate liability, because claimants often over-claim.
  2. *`int Version` over `[Timestamp] RowVersion`*: SQLite does not auto-generate rowversion values, so the service increments `Version` explicitly. A stale update throws `DbUpdateConcurrencyException`, which maps to HTTP 409.
  3. *Info requests as their own table*: the audit log is append-only history, while an info request is a stateful work item. Both are kept, because creating or answering a request also appends an activity.
  4. *No FX conversion*: summing across currencies without a rates source gives a misleading number. Per-currency totals are honest.
- **Challenged / Corrected**:
  1. *`HasDefaultValue(true)` on `IsActive`*: the first draft set a DB default. Caught on review: EF Core treats CLR `false` as "unset" for a property with a DB default, so it omits the column and an explicit `IsActive = false` would be silently stored as `true`. Removed the DB default; the C# initializer is the single source of truth. Also removed the redundant `HasDefaultValue(0)` on `Version`.
- **Out of scope (deliberate)**: Policy entity and coverage validation, document uploads, decision history beyond the audit log.
- **Verification**: `dotnet` was not on PATH in the AI's shell, so the developer ran the build and the migration manually (see docs/task-05-domain-extensions.md).

---

## Entry 05B — Repairing an Incomplete Task 05 (Model/Migration Drift)
- **Prompt**: Read the assessment brief, review progress, update the roadmap, then continue step by step (no commits; developer reviews and commits).
- **Finding**: The repo was inconsistent. The `AddLiabilityAndInformationRequests` migration and the updated model snapshot existed, and Entry 05 described the work as done. But `Claim.cs`, `AppUser.cs`, `ClaimsDbContext.cs`, `InformationRequest.cs` and `docs/task-05-domain-extensions.md` were missing or unchanged. The next `migrations add` would have generated a migration that **drops** the new columns.
- **Action**: Rebuilt the model code from the snapshot, which records the agreed design, instead of regenerating the migration. This kept the reviewed migration (including the `IsActive` backfill comment) unchanged.
- **Verification**: `dotnet-ef migrations has-pending-model-changes` reported "No changes have been made to the model since the last migration". Build: 0 warnings, 0 errors. Migration applied locally.
- **Lesson**: Check what is actually in the files and use EF's drift check before trusting a journal entry, whether the AI or I wrote it.
- **Also**: Added `docs/ROADMAP.md` with a requirement-to-step traceability table and the remaining steps (06–12).

---

## Entry 06 — Claims API Core (Task 06)
- **Prompt**: Build the first API slice: submit, list and view claims, with validation, simulated identity, seeded users and a consistent error contract. Workflow actions are deferred to Step 07.
- **Accepted**:
  - `ICurrentUserService` reads an `X-User-Id` header (seeded users with fixed ids). Real auth is out of scope for the timebox. The interface keeps the switch to JWT down to one class.
  - Domain exceptions (`NotFound`, `Forbidden`, `Unauthenticated`, `BusinessRule`) are mapped to ProblemDetails by an `IExceptionHandler`. `DbUpdateConcurrencyException` maps to 409. A 500 never returns exception text.
  - A claimant asking for another claimant's claim gets 404, not 403, so claim ids can't be probed.
  - Migrate and seed on startup so the app runs locally with a single command.
- **Changed from the earlier plan**:
  - *Currency derived from market* instead of the Task 03 doc's "validate the submitted ISO currency". Taking both from the client creates a consistency problem that a lookup avoids entirely.
- **Verified instead of trusting the AI**:
  - The AI claimed EF Core's SQLite provider cannot `ORDER BY` a `DateTimeOffset`, and used that to justify sorting in memory. I asked for proof: a throwaway console app on EF Core 10.0.15 threw `NotSupportedException`. Kept the in-memory sort, with the trade-off documented (no paging).
  - The first smoke test returned 404 for every route. The cause was an older API instance from my manual testing still holding port 5129, not a routing bug. Reran on port 5199 against a scratch database.
  - The first "403 for officer submit" test actually returned 400 because the test payload was invalid (`"KL"` is below the 3-character minimum). Fixed the test, not the code.
  - Noted a framework detail: `IValidatableObject.Validate` only runs after attribute validation passes, so cross-field errors don't appear alongside missing-field errors. This is standard ASP.NET behaviour and accepted.

---

## Entry 07 — Claim Workflow (Task 07)
- **Prompt**: Implement the claim lifecycle: pick up, reserve, request info, respond, approve, reject, settle. Use one transition table, optimistic concurrency via `Version`, and an audit entry for every action.
- **Accepted**:
  - `ClaimWorkflow` holds the transition table as data, and every action goes through `MoveTo()`. It is easy to read, easy to test (Step 10), and no action can skip it.
  - A fixed check order in every action (validation → role → exists → assigned → version → transition → rule), so the same mistake always gives the same status code.
  - Two-layer concurrency: an explicit `version` from the client catches a stale screen, and EF's `WHERE Version = @loaded` catches simultaneous requests.
  - Workflow actions live in a separate `ClaimWorkflowService`, which keeps `ClaimService` to submit and queries.
- **Decided / scoped down**:
  - Managers are read-only. Reassignment was left out to stay inside the timebox.
  - Only one open information request at a time, enforced by the transition table rather than an extra rule.
  - The question text and decision reason are not copied into activity descriptions. They live on their own records and would break the 1000-character activity limit.
- **Verified**:
  - I asked for a *real* race test, not just a stale-version test. Two parallel pick-up requests, 3 runs: one 200 and one 409 every time, and exactly 2 activities, which proves no duplicate audit entry gets through.
  - 22 scripted lifecycle scenarios (illegal transitions, wrong actor, wrong claimant, double answer, amount rules). All returned the expected codes, and the server log shows 0 unhandled errors.
  - EF drift check after adding the `ReserveSet` enum value: no migration needed (string conversion).

---

## Entry 08 — Team Dashboard & Liability Exposure (Task 08)
- **Prompt**: Build the staff dashboard: claims by status, the unassigned queue, per-officer workload and performance, and outstanding liability exposure.
- **Accepted**:
  - A single `GET /api/dashboard` for officers and managers (claimants get 403). The brief gives workload visibility to claims staff, not only managers.
  - The exposure formula is in `LiabilityExposure.For(claim)` and reuses `ClaimWorkflow.OpenStatuses`, so "open" has a single definition.
  - Exposure is grouped per market and currency, with `totalClaimed` beside it and `unreservedClaims` showing how much of the figure is still the claimant's own number.
  - Aggregation is in a pure `DashboardService.Build(...)`, which makes it unit-testable without a database.
  - Demo claims are seeded in every status so the dashboard isn't empty on first run.
- **Challenged / corrected**:
  - The AI's first justification for in-memory aggregation was "SQLite can't SUM decimals". I asked it to verify, as in Step 06. On EF Core 10.0.15 `SUM(decimal)` **works**, so that reason was dropped. The remaining reasons are testability and `DateTimeOffset` arithmetic, plus a documented scale trade-off.
  - Backdating seed activities: adding a `createdAt` parameter to the `ClaimActivity` constructor would have let any code backdate audit entries. Instead, only the seeder sets `CreatedAt` explicitly through EF's change tracker, and the domain constructor stays strict.
- **Verified**: Expected dashboard numbers were worked out by hand from the seed data before running. Every figure matched. I then settled a seeded claim with its seeded `version` (proving the seed versions are consistent with the workflow) and saw it drop out of exposure.

---

## Entry 09 — Kafka Claim Events & Notifications (Task 09)
- **Prompt**: Add Kafka with a defensible REST/Kafka split. Publish claim events after state changes, consume them into claimant notifications, provide `docker-compose.yml`, and keep the app runnable without Docker.
- **Decision (REST vs Kafka)**: REST for everything the user waits on (commands with validation and 409s, and all reads). Kafka for "something happened to a claim" side effects. The first consumer is claimant notifications (the brief's "receive decisions"). The frontend never talks to Kafka directly.
- **Accepted**:
  - One event per `ClaimActivity`, with `eventId` = activity id, so every event traces to exactly one audit record and doubles as an idempotency key.
  - Message key = claimId, for per-claim ordering.
  - Publish only after the commit, through a non-blocking producer (`Produce` + delivery callback, 10s timeout), so Kafka problems never fail or slow a user request.
  - At-least-once consumer (manual commit after the DB write) plus a unique `EventId` index, giving an idempotent effect.
  - `Kafka:Enabled` switch with a no-op publisher.
- **Challenged / corrected**:
  - The first run failed with `PendingModelChangesWarning`. I had run a DLL built before the migration was generated. The fix was to rebuild. EF's guard worked as intended, so it was not suppressed.
  - "Created Kafka topic" was missing from the logs. Cause: my own earlier test ran `kafka-console-consumer` against the topic, and the broker auto-created it with **1 partition**. I deleted the topic and re-ran to prove the app creates it with 3.
  - With the broker down, the consumer logged **174 warnings in 13s** (librdkafka reports every reconnect attempt). I added `KafkaLogging`: transient errors go to Debug, and only "all brokers down" and fatal errors are visible. That brought it down to 3 meaningful lines.
- **Known gap, accepted deliberately**: publish-after-commit is not atomic, so an event can be lost if the process dies after commit. The fix is the transactional outbox, documented and not built, to stay within the timebox.
- **Verified against the real broker** (apache/kafka 3.9.0): lifecycle, 6 ordered events, 5 notifications, lag 0; a 409 publishes nothing; a replay with a new consumer group creates no duplicates; broker down still returns 201 in 0.02s; the disabled mode works.

---

## Entry 10 — Automated Tests (Task 10)
- **Prompt**: Add an xUnit test project covering the business rules that matter most: lifecycle transitions, liability exposure, dashboard figures, concurrency, access rules and idempotent notifications.
- **Accepted**:
  - Two layers: fast pure-function tests (`ClaimWorkflow`, `LiabilityExposure`, `DashboardService.Build`, `ClaimNotificationHandler.BuildMessage`, `InformationRequest`), plus service tests through the real services.
  - Service tests run on **real SQLite in memory**, built by running the real migrations, with a fresh `DbContext` per call to mimic separate requests.
  - Hand-written fakes (`FixedCurrentUser`, `RecordingEventPublisher`) instead of adding a mocking library.
- **Challenged**:
  - *EF InMemory provider vs SQLite in memory*: InMemory is the usual quick choice, but it ignores concurrency tokens and foreign keys, so the most important test (concurrent write gives a conflict) would pass even when broken. I chose SQLite in memory.
  - *"57 passed on the first run" is not proof.* I asked for a mutation check: five deliberate bugs (concurrency token removed, illegal transition allowed, exposure formula broken, version check skipped, idempotency check dropped). Each one made the intended test fail, and every file was restored (`git diff` clean, 57/57 again).
- **Learned from the mutation check**: with the handler's "already handled" check removed, the unique `EventId` index still blocked the duplicate. The consumer therefore has two layers of idempotency, and a concurrent duplicate recovers through the retry loop.
- **Not covered (deliberate)**: HTTP-level tests via `WebApplicationFactory`, Kafka integration tests (verified manually in Task 09), frontend tests.
