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
