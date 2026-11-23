using Claims.Api.Enums;
using Claims.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Claims.Api.Data;

/// <summary>
/// Seeds demo data so the app is usable straight after `dotnet run`:
///  - users with fixed ids (usable as X-User-Id and by the frontend user switcher)
///  - a handful of claims in every lifecycle state, so the queue and dashboard are not empty.
/// Each part only runs when its table is empty.
/// </summary>
public static class DbSeeder
{
    public static readonly Guid JaneTanId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid ArjunKumarId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    public static readonly Guid DavidWongId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static readonly Guid MeiLinId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    public static readonly Guid SarahLimId = Guid.Parse("30000000-0000-0000-0000-000000000001");

    public static async Task SeedAsync(ClaimsDbContext db, CancellationToken cancellationToken = default)
    {
        if (!await db.Users.AnyAsync(cancellationToken))
        {
            db.Users.AddRange(
                new AppUser { Id = JaneTanId, FullName = "Jane Tan", Email = "jane.tan@example.com", Role = UserRole.Claimant },
                new AppUser { Id = ArjunKumarId, FullName = "Arjun Kumar", Email = "arjun.kumar@example.com", Role = UserRole.Claimant },
                new AppUser { Id = DavidWongId, FullName = "David Wong", Email = "david.wong@example.com", Role = UserRole.ClaimsOfficer },
                new AppUser { Id = MeiLinId, FullName = "Mei Lin", Email = "mei.lin@example.com", Role = UserRole.ClaimsOfficer },
                new AppUser { Id = SarahLimId, FullName = "Sarah Lim", Email = "sarah.lim@example.com", Role = UserRole.Manager });
            await db.SaveChangesAsync(cancellationToken);
        }

        if (!await db.Claims.AnyAsync(cancellationToken))
        {
            SeedDemoClaims(db, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static void SeedDemoClaims(ClaimsDbContext db, DateTimeOffset now)
    {
        DateTimeOffset DaysAgo(double days) => now.AddDays(-days);

        // 1. Waiting in the queue
        var c1 = NewClaim(db, "DEMO0001", JaneTanId, ClaimType.Motor, "SG", 5000m, DaysAgo(3),
            "Rear-ended while stopped at a traffic light.", "Orchard Road, Singapore");

        // 2. Waiting in the queue
        var c2 = NewClaim(db, "DEMO0002", ArjunKumarId, ClaimType.Property, "MY", 12000m, DaysAgo(1),
            "Burst pipe flooded the kitchen.", "Petaling Jaya, Selangor");

        // 3. Under review with a reserve set
        var c3 = NewClaim(db, "DEMO0003", JaneTanId, ClaimType.Property, "HK", 80000m, DaysAgo(6),
            "Typhoon damage to roof and windows.", "Sai Kung, Hong Kong");
        PickUp(db, c3, DavidWongId, "David Wong", DaysAgo(5));
        c3.ReserveAmount = 65000m;
        Activity(db, c3, ClaimActivityType.ReserveSet, "Reserve set to HKD 65,000.00 by David Wong.", DavidWongId, DaysAgo(4.5));

        // 4. Waiting on the claimant
        var c4 = NewClaim(db, "DEMO0004", ArjunKumarId, ClaimType.Motor, "SG", 3200m, DaysAgo(4),
            "Side mirror and door damaged in car park.", "Jurong East, Singapore");
        PickUp(db, c4, MeiLinId, "Mei Lin", DaysAgo(3));
        c4.Status = ClaimStatus.AdditionalInformationRequired;
        var request = new InformationRequest(c4.Id, MeiLinId, "Please upload photos of the damage and the car park ticket.");
        db.InformationRequests.Add(request);
        db.Entry(request).Property(r => r.CreatedAt).CurrentValue = DaysAgo(2.5);
        Activity(db, c4, ClaimActivityType.InformationRequested, "Mei Lin requested additional information from the claimant.", MeiLinId, DaysAgo(2.5));

        // 5. Approved, payment pending (still counts as exposure)
        var c5 = NewClaim(db, "DEMO0005", JaneTanId, ClaimType.Motor, "AU", 9000m, DaysAgo(10),
            "Hail damage to bonnet and windscreen.", "Parramatta, NSW");
        PickUp(db, c5, DavidWongId, "David Wong", DaysAgo(9));
        Decide(db, c5, ClaimStatus.Approved, 8500m, "Covered; excess of AUD 500 deducted.",
            "Claim approved for AUD 8,500.00 by David Wong.", DavidWongId, DaysAgo(2));

        // 6. Rejected
        var c6 = NewClaim(db, "DEMO0006", ArjunKumarId, ClaimType.Property, "TH", 150000m, DaysAgo(12),
            "Flood damage to ground floor.", "Bang Kapi, Bangkok");
        PickUp(db, c6, MeiLinId, "Mei Lin", DaysAgo(11));
        Decide(db, c6, ClaimStatus.Rejected, null, "Flood is excluded under this policy.",
            "Claim rejected by Mei Lin.", MeiLinId, DaysAgo(5));

        // 7. Settled
        var c7 = NewClaim(db, "DEMO0007", JaneTanId, ClaimType.Property, "SG", 2500m, DaysAgo(20),
            "Laptop stolen during break-in.", "Tampines, Singapore");
        PickUp(db, c7, MeiLinId, "Mei Lin", DaysAgo(19));
        Decide(db, c7, ClaimStatus.Approved, 2500m, "Theft confirmed by police report.",
            "Claim approved for SGD 2,500.00 by Mei Lin.", MeiLinId, DaysAgo(15));
        c7.Status = ClaimStatus.Settled;
        Activity(db, c7, ClaimActivityType.StatusChanged, "Claim settled; payment of SGD 2,500.00 released by Mei Lin.", MeiLinId, DaysAgo(14));

        // Version = number of changes after submission, matching what the workflow would produce.
        foreach (var claim in new[] { c1, c2, c3, c4, c5, c6, c7 })
        {
            claim.Version = db.ChangeTracker.Entries<ClaimActivity>().Count(e => e.Entity.ClaimId == claim.Id) - 1;
            claim.UpdatedAt = db.ChangeTracker.Entries<ClaimActivity>()
                .Where(e => e.Entity.ClaimId == claim.Id)
                .Max(e => e.Entity.CreatedAt);
        }
    }

    private static Claim NewClaim(ClaimsDbContext db, string suffix, Guid claimantId, ClaimType type, string market,
        decimal amount, DateTimeOffset createdAt, string description, string location)
    {
        var claim = new Claim
        {
            Id = Guid.NewGuid(),
            ClaimNumber = $"CLM-{market}-{createdAt.Year}-{suffix}",
            ClaimantId = claimantId,
            ClaimType = type,
            Description = description,
            IncidentLocation = location,
            IncidentDate = createdAt.AddDays(-1),
            ClaimedAmount = amount,
            MarketCode = market,
            CurrencyCode = Markets.CurrencyByMarket[market],
            Status = ClaimStatus.Submitted,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
        db.Claims.Add(claim);
        var claimantName = claimantId == JaneTanId ? "Jane Tan" : "Arjun Kumar";
        Activity(db, claim, ClaimActivityType.ClaimSubmitted, $"Claim submitted by {claimantName}.", claimantId, createdAt);
        return claim;
    }

    private static void PickUp(ClaimsDbContext db, Claim claim, Guid officerId, string officerName, DateTimeOffset at)
    {
        claim.Status = ClaimStatus.UnderReview;
        claim.AssignedOfficerId = officerId;
        Activity(db, claim, ClaimActivityType.ClaimAssigned, $"Picked up by {officerName}; claim is now under review.", officerId, at);
    }

    private static void Decide(ClaimsDbContext db, Claim claim, ClaimStatus status, decimal? approvedAmount,
        string reason, string activityText, Guid officerId, DateTimeOffset at)
    {
        claim.Status = status;
        claim.ApprovedAmount = approvedAmount;
        claim.DecisionReason = reason;
        claim.DecidedAt = at;
        Activity(db, claim, ClaimActivityType.DecisionRecorded, activityText, officerId, at);
    }

    /// <summary>
    /// ClaimActivity.CreatedAt is private (application code cannot backdate audit entries).
    /// Only the seeder backdates, and it does so explicitly through EF's change tracker.
    /// </summary>
    private static void Activity(ClaimsDbContext db, Claim claim, ClaimActivityType type, string description, Guid actorId, DateTimeOffset at)
    {
        var activity = new ClaimActivity(claim.Id, type, description, actorId);
        db.ClaimActivities.Add(activity);
        db.Entry(activity).Property(a => a.CreatedAt).CurrentValue = at;
    }
}
