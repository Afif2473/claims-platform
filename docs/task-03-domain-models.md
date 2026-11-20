# Task 03 — Claims Management Domain Models

This document details the initial domain models and enums created for the Claims Management System, along with beginner-friendly explanations of core C# object-oriented concepts and architectural decisions.

---

## 1. Domain Entities and Enums Overview

### Enums (`src/Claims.Api/Enums/`)

| Enum | Values | Purpose |
| :--- | :--- | :--- |
| `UserRole` | `Claimant`, `ClaimsOfficer`, `Manager` | Distinguishes between customers filing claims, adjusters assessing them, and managers monitoring operations. |
| `ClaimType` | `Motor`, `Property` | Categorises the insurance incident type across the insurer's primary lines of business. |
| `ClaimStatus` | `Submitted`, `UnderReview`, `AdditionalInformationRequired`, `Approved`, `Rejected`, `Settled` | Tracks the entire claim lifecycle from first notice of loss to financial settlement or rejection. |
| `ClaimActivityType` | `ClaimSubmitted`, `ClaimAssigned`, `StatusChanged`, `InformationRequested`, `InformationProvided`, `DecisionRecorded` | Classifies events in the claim's audit and timeline history. |

---

### Models (`src/Claims.Api/Models/`)

#### 1. `AppUser`
Represents any actor interacting with the system.

```csharp
namespace Claims.Api.Models;

public class AppUser
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public ICollection<Claim> SubmittedClaims { get; set; } = new List<Claim>();
    public ICollection<Claim> AssignedClaims { get; set; } = new List<Claim>();
}
```

#### 2. `Claim`
Represents the core insurance claim record.

```csharp
namespace Claims.Api.Models;

public class Claim
{
    public Guid Id { get; set; }
    public string ClaimNumber { get; set; } = string.Empty;
    public Guid ClaimantId { get; set; }
    public Guid? AssignedOfficerId { get; set; }
    public ClaimType ClaimType { get; set; }
    public string Description { get; set; } = string.Empty;
    public string IncidentLocation { get; set; } = string.Empty;
    public DateTimeOffset IncidentDate { get; set; }
    public decimal ClaimedAmount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string MarketCode { get; set; } = string.Empty;
    public ClaimStatus Status { get; set; } = ClaimStatus.Submitted;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public AppUser? Claimant { get; set; }
    public AppUser? AssignedOfficer { get; set; }
    public ICollection<ClaimActivity> Activities { get; set; } = new List<ClaimActivity>();
}
```

#### 3. `ClaimActivity` (Application-Level Append-Only Audit Trail)
Represents an individual event or milestone in a claim's timeline for auditability. Properties use `private set;` and require construction through a constructor to provide application-level protection against ordinary code modifying existing audit records, encouraging an append-only pattern.

```csharp
namespace Claims.Api.Models;

public class ClaimActivity
{
    public Guid Id { get; private set; }
    public Guid ClaimId { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public ClaimActivityType ActivityType { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    // Navigation properties
    public Claim? Claim { get; private set; }
    public AppUser? ActorUser { get; private set; }

    // Required by EF Core
    protected ClaimActivity() { }

    public ClaimActivity(Guid claimId, ClaimActivityType activityType, string description, Guid? actorUserId = null)
    {
        Id = Guid.NewGuid();
        ClaimId = claimId;
        ActivityType = activityType;
        Description = string.IsNullOrWhiteSpace(description) ? throw new ArgumentException("Description cannot be empty.", nameof(description)) : description;
        ActorUserId = actorUserId;
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
```

---

## 2. Beginner C# & Domain Modeling Guide

### 1. What is a C# Class?
A **class** is a blueprint or template for creating objects. For example, the `Claim` class defines what pieces of information every claim must have (such as a claim number, status, and amount). When a claimant submits a specific incident, the application creates an *instance* (an object) of that class containing the claimant's real data.

### 2. What is a Property (`{ get; set; }`)?
In C#, a **property** is a member of a class that provides a secure, flexible way to read and write an object's data:
- `get`: reads the value.
- `set`: assigns a new value.
Using `{ get; private set; }` (as in `ClaimActivity`) makes the property read-only to external callers, protecting the object against accidental modification by ordinary application code once instantiated.

### 3. Why use `Guid` instead of integer IDs?
A `Guid` (Globally Unique Identifier, also known as UUID) is a 128-bit number formatted like `3f2504e0-4f89-11d3-9a0c-0305e82c3301`.
- **Decentralized generation**: Any service, frontend client, or background worker can generate a unique `Guid` immediately without having to query the database first to find the next number (`1, 2, 3...`).
- **Security & Obscurity**: Sequential IDs like `/api/claims/101` allow users to guess neighboring claim IDs (`/api/claims/102`). Guids are practically impossible to guess.
- **Microservices & Event-driven readiness**: If claims are passed via Kafka or background queues in the future, Guids prevent ID collisions.

### 4. Why does `ClaimedAmount` use `decimal` instead of `float` or `double`?
- `float` and `double` are *binary floating-point* numbers. They represent numbers using powers of 2, which leads to tiny rounding errors (e.g., `0.1 + 0.2 = 0.30000000000000004`). In financial calculations, rounding errors compound and can cause audit failures.
- `decimal` is a 128-bit *base-10 floating-point* type designed specifically for financial and monetary calculations. It guarantees exact decimal representation up to 28-29 significant digits.

### 5. Why is `AssignedOfficerId` nullable (`Guid?`)?
The question mark `?` indicates a **nullable value type**:
- When a claimant first reports an incident, the claim is in `Submitted` status and sits in the unassigned pool. No officer has picked it up yet.
- Therefore, `AssignedOfficerId` has no value (`null`).
- Once a claims officer accepts the claim, `AssignedOfficerId` is updated with that officer's `Guid`.

### 6. What are Navigation Properties?
- While `ClaimantId` stores the raw foreign key identifier (e.g. `Guid`), the navigation property `public AppUser? Claimant { get; set; }` lets you navigate directly to the related `AppUser` object in C# code.
- This allows you to write natural expressions such as `claim.Claimant.FullName` or `claim.Activities.Count` without manually performing separate database lookups.

### 7. How `ClaimantId` and `AssignedOfficerId` relate to `AppUser`
Both IDs reference the `AppUser` entity, but represent distinct business relationships:
- `ClaimantId`: The policyholder / customer who experienced the loss and submitted the claim. Every claim **must** have a claimant (`Guid` is non-nullable).
- `AssignedOfficerId`: The insurer's internal employee tasked with investigating and settling the claim. A claim starts without an officer (`Guid?` is nullable).
- *Entity Framework Note*: In Task 04 (DbContext setup), we will use EF Core's Fluent API (`HasOne(...).WithMany(...).HasForeignKey(...)`) to explicitly configure these two distinct relationships so EF Core doesn't conflate them.

### 8. Why `ClaimActivity` is essential for insurance claims
The brief notes that the insurer's current process is fragmented, leaving claimants with no visibility and managers with no clear view of outstanding work.
`ClaimActivity` acts as an **application-level append-only audit log and event timeline**:
- With `private set` and constructor initialization, it provides application-level protection against ordinary code modifying existing audit records.
- Every time a state transition occurs (e.g., status moves to `UnderReview`, info requested, decision approved), a `ClaimActivity` record is appended.
- Claimants can view a clear timeline: *"Officer Jane requested photos on Nov 20"*, *"Claim approved on Nov 22"*.
- Internal auditors have a reliable historical record of who took which action and when.
- Setting `ActorUserId` as nullable (`Guid?`) allows the system to log automated events (e.g. system auto-assignment or SLA reminders) where no human user was the actor.

---

## 3. Concrete Example: Relationship in Action

Here is an illustrative example showing how these models connect in C# memory:

```csharp
// 1. Create a Claimant user
var claimant = new AppUser
{
    Id = Guid.NewGuid(),
    FullName = "Jane Tan",
    Email = "jane.tan@example.com",
    Role = UserRole.Claimant
};

// 2. Create a Claims Officer user
var officer = new AppUser
{
    Id = Guid.NewGuid(),
    FullName = "David Wong",
    Email = "david.wong@example.com",
    Role = UserRole.ClaimsOfficer
};

// 3. Create a Claim submitted by Jane
var claim = new Claim
{
    Id = Guid.NewGuid(),
    ClaimNumber = "CLM-2026-SG-0001",
    ClaimantId = claimant.Id,
    Claimant = claimant,
    AssignedOfficerId = officer.Id,
    AssignedOfficer = officer,
    ClaimType = ClaimType.Motor,
    MarketCode = "SG",
    CurrencyCode = "SGD",
    ClaimedAmount = 4500.00m,
    IncidentLocation = "Orchard Road, Singapore",
    IncidentDate = DateTimeOffset.UtcNow.AddDays(-2),
    Status = ClaimStatus.UnderReview,
    Description = "Minor collision at traffic intersection."
};

// 4. Attach audit trail activities using the parameterized constructor
claim.Activities.Add(new ClaimActivity(
    claimId: claim.Id,
    activityType: ClaimActivityType.ClaimSubmitted,
    description: "Claim submitted by Jane Tan via portal.",
    actorUserId: claimant.Id
));

claim.Activities.Add(new ClaimActivity(
    claimId: claim.Id,
    activityType: ClaimActivityType.ClaimAssigned,
    description: "Claim assigned to David Wong.",
    actorUserId: officer.Id
));

claim.Activities.Add(new ClaimActivity(
    claimId: claim.Id,
    activityType: ClaimActivityType.StatusChanged,
    description: "Status changed from Submitted to UnderReview.",
    actorUserId: officer.Id
));
```

---

## 4. Architectural Layering: Validation Strategy

As identified during review, validation is intentionally separated across layers rather than placing business rules directly on domain entities:

1. **API / DTO Layer (Incoming Contract Validation)**:
   - Request DTOs validate incoming payload integrity before reaching controllers:
     - `ClaimedAmount > 0`
     - Valid 3-letter currency code (e.g., ISO-4217: `SGD`, `MYR`, `AUD`, `HKD`, `THB`, `PHP`)
     - Non-empty description and incident location
2. **Service Layer (Domain & Workflow Validation)**:
   - Validates that `MarketCode` is one of the six supported APAC markets.
   - Enforces lifecycle status transition rules (e.g., a claim cannot transition to `Settled` without first being `Approved`).
   - Ensures an officer is assigned before status can transition to `UnderReview`.
3. **Domain Entity Layer (Data Model State)**:
   - Holds verified state, relationship mappings, and foreign key constraints.
