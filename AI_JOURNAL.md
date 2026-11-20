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
