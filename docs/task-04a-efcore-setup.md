# Task 04A — Entity Framework Core & SQLite Configuration

This document outlines the setup of Entity Framework Core with SQLite for the Claims Management System, including DbContext configuration, Fluent API relationship definitions, index/constraint strategies, deletion behavior rules, and beginner concepts.

---

## 1. Packages Installed

| Package | Version | Purpose |
| :--- | :--- | :--- |
| `Microsoft.EntityFrameworkCore.Sqlite` | `10.0.15` | SQLite database provider for EF Core, bundling native SQLite runtime binaries (`e_sqlite3`). |
| `Microsoft.EntityFrameworkCore.Design` | `10.0.15` | Tooling support for EF Core migrations and reverse engineering (`PrivateAssets="all"`). |

*Note on package versioning*: .NET 10 SDK (`net10.0`) requires matching 10.x packages. Both packages were explicitly aligned to `10.0.15` matching `Microsoft.AspNetCore.OpenApi`.

*Note on `dotnet-ef`*: Checked global tool registry; `dotnet-ef` is not currently installed. It will be required when running migration commands in Task 04B.

---

## 2. DbContext Implementation (`src/Claims.Api/Data/ClaimsDbContext.cs`)

`ClaimsDbContext` derives from `DbContext` and provides three `DbSet` collections:
- `DbSet<AppUser> Users`
- `DbSet<Claim> Claims`
- `DbSet<ClaimActivity> ClaimActivities`

### Explicit Relationship Configurations (`OnModelCreating`)

```csharp
// 1. Claimant Relationship (Required 1-to-many)
// One AppUser can submit many Claims.
modelBuilder.Entity<Claim>()
    .HasOne(c => c.Claimant)
    .WithMany(u => u.SubmittedClaims)
    .HasForeignKey(c => c.ClaimantId)
    .IsRequired()
    .OnDelete(DeleteBehavior.Restrict);

// 2. Assigned Officer Relationship (Optional 1-to-many)
// One AppUser can be assigned many Claims as an adjuster.
modelBuilder.Entity<Claim>()
    .HasOne(c => c.AssignedOfficer)
    .WithMany(u => u.AssignedClaims)
    .HasForeignKey(c => c.AssignedOfficerId)
    .IsRequired(false)
    .OnDelete(DeleteBehavior.SetNull);

// 3. Claim Activities (Required 1-to-many)
// One Claim has many ClaimActivity timeline entries.
modelBuilder.Entity<Claim>()
    .HasMany(c => c.Activities)
    .WithOne(a => a.Claim)
    .HasForeignKey(a => a.ClaimId)
    .IsRequired()
    .OnDelete(DeleteBehavior.Restrict);

// 4. Activity Actor (Optional reference)
// Refers to AppUser who performed the action (null indicates system-generated).
modelBuilder.Entity<ClaimActivity>()
    .HasOne(a => a.ActorUser)
    .WithMany()
    .HasForeignKey(a => a.ActorUserId)
    .IsRequired(false)
    .OnDelete(DeleteBehavior.SetNull);
```

---

## 3. Deletion Behavior Rationale

In an insurance claims system, data retention and auditability are critical operational considerations. We configured foreign key deletion behaviors to protect against specific accidental data-loss scenarios (such as an ORM cascading delete):

| Relationship | Behavior | Accidental Loss Protection |
| :--- | :--- | :--- |
| **Claimant → Claim** | `DeleteBehavior.Restrict` | If a claimant account is deleted, their submitted insurance claims **are not** cascade-deleted. The database blocks the deletion, protecting claims records from accidental removal. |
| **Assigned Officer → Claim** | `DeleteBehavior.SetNull` | If an assigned claims officer is deleted, the claim is not deleted; instead, `AssignedOfficerId` is set to `null` so the claim remains in the backlog for reassignment. |
| **Claim → ClaimActivity** | `DeleteBehavior.Restrict` | Blocks deletion of a claim if audit activity logs exist, preventing accidental cascading removal of timeline entries. |
| **ActorUser → ClaimActivity** | `DeleteBehavior.SetNull` | If an actor user is deleted, the historical audit log entry remains intact with `ActorUserId = null`, preserving the timeline description and timestamp. |

> [!NOTE]
> **Production vs. Local MVP Retention**:
> While `Restrict` and `SetNull` guard against accidental ORM-level cascading deletions, they do **not** by themselves guarantee legal regulatory retention or a tamper-proof audit trail (they do not prevent direct administrative deletes or manual database updates).
> In production enterprise systems, hard-deleting user accounts is generally avoided entirely. Production systems favor **user deactivation** (e.g. `IsActive = false` or soft-delete flags) so that historical identity attribution is permanently preserved alongside all audit records.

---

## 4. Database Constraints & Indexing Strategy

### Constraints
- **Primary Keys**: Configured on all entities (`Id`).
- **String Lengths**:
  - `AppUser.FullName`: 150 chars max
  - `AppUser.Email`: 254 chars max (RFC 5321 standard)
  - `Claim.ClaimNumber`: 50 chars max
  - `Claim.Description`: 2000 chars max
  - `Claim.IncidentLocation`: 250 chars max
  - `Claim.CurrencyCode`: 3 chars max (ISO-4217 standard)
  - `Claim.MarketCode`: 10 chars max
  - `ClaimActivity.Description`: 1000 chars max
- **Monetary Precision**: `Claim.ClaimedAmount` is configured with `HasPrecision(18, 2)`. This explicitly documents the intended domain precision and schema metadata. However, SQLite uses dynamic column typing (type affinity) and does not strictly enforce decimal precision or scale at the database engine level like SQL Server or PostgreSQL. Therefore, application-level validation (in DTOs and service logic) is required to enforce valid monetary bounds and decimal formatting.
- **Enum Storage**: `Role`, `ClaimType`, `Status`, and `ActivityType` use `.HasConversion<string>()` to ensure transparent, human-readable database columns that are immune to enum order changes.

### Indexes
- **Unique Indexes**:
  - `Claim.ClaimNumber` (Unique business key)
  - `AppUser.Email` (Unique user login/identity)
- **Performance Indexes**:
  - `Claim.Status`: Fast filtering of incoming claims (`Submitted`, `UnderReview`).
  - `Claim.MarketCode`: Fast segmentation by the 6 APAC markets.
  - `Claim.AssignedOfficerId`: Quick retrieval of an officer's assigned workload.
  - `Claim.CreatedAt`: Efficient chronological sorting and SLA tracking.
  - `ClaimActivity.ClaimId`: Fast retrieval of a claim's timeline.
  - `ClaimActivity.CreatedAt`: Sorting activity timeline chronologically.

---

## 5. Dependency Injection Registration

In [appsettings.json](../src/Claims.Api/appsettings.json):
```json
{
  "ConnectionStrings": {
    "ClaimsDatabase": "Data Source=claims.db"
  }
}
```

In [Program.cs](../src/Claims.Api/Program.cs):
```csharp
builder.Services.AddDbContext<ClaimsDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("ClaimsDatabase")));
```

---

## 6. Beginner Guide: Entity Framework Core Concepts

### What is Entity Framework Core (EF Core)?
EF Core is an **Object-Relational Mapper (ORM)**. In software development, your C# code thinks in terms of objects and classes (`Claim`, `AppUser`), while the database thinks in terms of tables, rows, and columns. EF Core bridges this gap: it translates your C# queries into SQL queries (`SELECT`, `INSERT`, `UPDATE`, `DELETE`) and maps the returned database rows back into C# objects automatically.

### What is a `DbContext`?
The `DbContext` represents your application's session with the database. It:
1. Manages database connections.
2. Configures model-to-table mappings (`OnModelCreating`).
3. Tracks changes made to objects in memory (Change Tracker).
4. Persists changes to the database when calling `SaveChangesAsync()`.

### What does a `DbSet<T>` represent?
A `DbSet<T>` represents a specific table in the database and provides LINQ querying capabilities over that entity. For example, `ClaimsDbContext.Claims` lets you write `await db.Claims.Where(c => c.Status == ClaimStatus.Submitted).ToListAsync()`, which EF Core converts to `SELECT * FROM Claims WHERE Status = 'Submitted'`.

### What does Fluent API configuration achieve?
EF Core allows configuration using either *Data Annotations* (attributes above properties like `[MaxLength(50)]`) or the **Fluent API** (method calls inside `OnModelCreating`). Fluent API is the industry standard because:
1. It keeps domain model classes clean and free of database-specific attributes (Separation of Concerns).
2. It supports advanced configurations that annotations cannot express (e.g. Disambiguating two foreign keys to the same table, configuring deletion behaviors, setting composite or filtered indexes).

### Why SQLite is suitable for this local MVP
1. **Serverless & Zero Configuration**: SQLite runs as an in-process library embedded directly in the application. It requires no external database server, no Docker container, and no credentials/ports.
2. **Single-File Portability**: The entire database is stored in a single file (`claims.db`). It starts instantly, can be inspected with GUI tools like DB Browser for SQLite, and is wiped or reset easily during testing.
3. **Full SQL & EF Core Feature Set**: SQLite supports transactions, foreign keys, constraints, and ACID compliance, making it ideal for assessment scenarios, rapid prototyping, and automated testing.

---

## 7. Verification & Build

- **Build Command**: `dotnet build ClaimsManagement.slnx`
- **Output**:
  ```text
  Claims.Api -> src/Claims.Api/bin/Debug/net10.0/Claims.Api.dll
  Build succeeded.
      0 Warning(s)
      0 Error(s)
  ```
- **Migration Status**: As requested, no migrations have been created or applied in Task 04A.
