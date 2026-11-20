# Task 04B — Initial EF Core Database Migration

This document records the creation, inspection, and application of the initial Entity Framework Core migration (`InitialCreate`) for the Claims Management System.

---

## 1. Setup & Tooling Configuration

### Tool Manifest & CLI Installation
Before generating migrations, a local .NET tool manifest was created and `dotnet-ef` was installed locally, pinned strictly to version `10.0.15` to match the SDK and project packages:

```bash
# 1. Create local tool manifest
dotnet new tool-manifest

# 2. Install dotnet-ef version 10.0.15 locally
dotnet tool install dotnet-ef --version 10.0.15

# 3. Verify installed version
dotnet tool run dotnet-ef --version
# Output: 10.0.15
```

This generated `dotnet-tools.json`:
```json
{
  "version": 1,
  "isRoot": true,
  "tools": {
    "dotnet-ef": {
      "version": "10.0.15",
      "commands": [
        "dotnet-ef"
      ],
      "rollForward": false
    }
  }
}
```

---

## 2. Migration Generation (`InitialCreate`)

The initial migration was generated using the local `dotnet-ef` tool:

```bash
dotnet tool run dotnet-ef migrations add InitialCreate \
  --project src/Claims.Api/Claims.Api.csproj \
  --startup-project src/Claims.Api/Claims.Api.csproj \
  --output-dir Data/Migrations
```

This created the following files in `src/Claims.Api/Data/Migrations/`:
- `20261120085439_InitialCreate.cs`: The executable schema operations (`Up` and `Down`).
- `20261120085439_InitialCreate.Designer.cs`: Metadata for the migration.
- `ClaimsDbContextModelSnapshot.cs`: The current snapshot of the database model used to calculate future schema diffs.

---

## 3. Pre-Application Migration Inspection

The generated migration code was carefully inspected before applying it to the database:

### Tables Created
1. **`Users`**: Primary key `Id` (Guid stored as `TEXT`), `FullName` (max 150), `Email` (max 254), `Role` (string enum, max 30), `CreatedAt` (DateTimeOffset as `TEXT`).
2. **`Claims`**: Primary key `Id` (Guid as `TEXT`), `ClaimNumber` (max 50), `ClaimantId` (Guid), `AssignedOfficerId` (nullable Guid), `ClaimType` (string enum, max 30), `Description` (max 2000), `IncidentLocation` (max 250), `IncidentDate`, `ClaimedAmount` (`TEXT`, precision 18, scale 2), `CurrencyCode` (max 3), `MarketCode` (max 10), `Status` (string enum, max 50), `CreatedAt`, `UpdatedAt`.
3. **`ClaimActivities`**: Primary key `Id` (Guid as `TEXT`), `ClaimId` (Guid), `ActorUserId` (nullable Guid), `ActivityType` (string enum, max 50), `Description` (max 1000), `CreatedAt`.

### Relationships & Referential Actions
- **`FK_Claims_Users_ClaimantId`**: Required foreign key to `Users(Id)` with `onDelete: ReferentialAction.Restrict`. Blocks deleting a claimant if they have active claims.
- **`FK_Claims_Users_AssignedOfficerId`**: Optional foreign key to `Users(Id)` with `onDelete: ReferentialAction.SetNull`. Unassigns the officer without deleting the claim.
- **`FK_ClaimsActivities_Claims_ClaimId`**: Required foreign key to `Claims(Id)` with `onDelete: ReferentialAction.Restrict`. Blocks deleting a claim while historical activities exist.
- **`FK_ClaimsActivities_Users_ActorUserId`**: Optional foreign key to `Users(Id)` with `onDelete: ReferentialAction.SetNull`. Preserves audit log entries when an actor user is removed.

### Indexes Verified
- **Unique Indexes**:
  - `IX_Users_Email` on `Users(Email)` (`unique: true`)
  - `IX_Claims_ClaimNumber` on `Claims(ClaimNumber)` (`unique: true`)
- **Query Performance Indexes**:
  - `IX_Claims_Status` on `Claims(Status)`
  - `IX_Claims_MarketCode` on `Claims(MarketCode)`
  - `IX_Claims_AssignedOfficerId` on `Claims(AssignedOfficerId)`
  - `IX_Claims_ClaimantId` on `Claims(ClaimantId)`
  - `IX_Claims_CreatedAt` on `Claims(CreatedAt)`
  - `IX_ClaimActivities_ClaimId` on `ClaimActivities(ClaimId)`
  - `IX_ClaimActivities_CreatedAt` on `ClaimActivities(CreatedAt)`
  - `IX_ClaimActivities_ActorUserId` on `ClaimActivities(ActorUserId)`

---

## 4. Applying the Migration

The migration was applied to the SQLite database:

```bash
dotnet tool run dotnet-ef database update \
  --project src/Claims.Api/Claims.Api.csproj \
  --startup-project src/Claims.Api/Claims.Api.csproj
```

EF Core executed the DDL commands, enabled SQLite Write-Ahead Logging (`PRAGMA journal_mode = 'wal'`), created `__EFMigrationsLock` and `__EFMigrationsHistory`, and recorded:
```sql
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261120085439_InitialCreate', '10.0.15');
```

---

## 5. Verification & Validation

1. **Database File Creation**:
   - Database file created at `src/Claims.Api/claims.db`.
   - Verified that `git status` ignores `claims.db` via `.gitignore` (`*.db`), so no binary database files are tracked in Git.
2. **Migration List**:
   - `dotnet tool run dotnet-ef migrations list --project src/Claims.Api/Claims.Api.csproj --startup-project src/Claims.Api/Claims.Api.csproj --no-build`
   - Output: `20261120085439_InitialCreate` (Recognized and applied).
3. **Build Status**:
   - `dotnet build ClaimsManagement.slnx`
   - Output: **`Build succeeded. 0 Warning(s), 0 Error(s)`**.

---

## 6. Beginner Concepts: EF Core Migrations

### What is a Migration?
As software evolves, domain models change (new properties, new relationships, changed data types). A **migration** is a set of C# instructions that tells EF Core how to transition a database schema from one version to another:
- **`Up()`**: Applies the schema changes (e.g., `CreateTable`, `AddColumn`, `CreateIndex`).
- **`Down()`**: Reverses those exact changes (e.g., `DropTable`, `DropColumn`) if you ever need to roll back.

### Why use Migrations instead of `EnsureCreated()`?
- `context.Database.EnsureCreated()` creates the database if it doesn't exist, but it **cannot evolve** existing schemas when models change later. It also bypasses migration tracking entirely.
- Migrations maintain a structured, version-controlled history of schema changes in code, allowing safe upgrades across local, test, and production environments.

### What is `__EFMigrationsHistory`?
EF Core creates an internal metadata table in your database named `__EFMigrationsHistory`. Every time you run `database update`, EF Core checks this table to see which migrations have already been applied and executes only the newer ones.

---

## 7. Smoke Test Results

A live end-to-end smoke test was performed against the running API:

1. **Startup**: Started using the standard HTTP profile (`http://localhost:5129`).
2. **Database Connectivity**: Verified via `ClaimsDbContext.Database.CanConnectAsync()` injected into `HealthController`.
3. **HTTP Health Check**:
   - `GET http://localhost:5129/api/health`
   - **Status Code**: `HTTP 200 OK`
   - **Response Payload**:
     ```json
     {
       "status": "Healthy",
       "database": "Connected",
       "service": "Claims Management API",
       "environment": "Development",
       "timestamp": "2026-11-20T09:28:59.1374802Z"
     }
     ```
4. **Shutdown**: Clean termination via `SIGTERM` signal.
5. **Post-Test Verification**:
   - Database file `src/Claims.Api/claims.db` remains intact (`80 KB`).
   - `dotnet tool run dotnet-ef migrations list` confirmed `20261120085439_InitialCreate` applied.
   - `dotnet build ClaimsManagement.slnx` passed with **0 Warning(s), 0 Error(s)**.
