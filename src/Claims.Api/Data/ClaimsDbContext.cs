using Claims.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Claims.Api.Data;

/// <summary>
/// Entity Framework Core database context for the Claims Management System.
/// Manages entity configuration, relationship mappings, and database access.
/// </summary>
public class ClaimsDbContext : DbContext
{
    public ClaimsDbContext(DbContextOptions<ClaimsDbContext> options)
        : base(options)
    {
    }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Claim> Claims => Set<Claim>();
    public DbSet<ClaimActivity> ClaimActivities => Set<ClaimActivity>();
    public DbSet<InformationRequest> InformationRequests => Set<InformationRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ==========================================
        // AppUser Configuration
        // ==========================================
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasKey(u => u.Id);

            entity.Property(u => u.FullName)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(u => u.Email)
                .HasMaxLength(254)
                .IsRequired();

            entity.Property(u => u.Role)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(u => u.CreatedAt)
                .IsRequired();

            // Unique index on Email to guarantee uniqueness across users
            entity.HasIndex(u => u.Email)
                .IsUnique();
        });

        // ==========================================
        // Claim Configuration
        // ==========================================
        modelBuilder.Entity<Claim>(entity =>
        {
            entity.HasKey(c => c.Id);

            entity.Property(c => c.ClaimNumber)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(c => c.ClaimType)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(c => c.Description)
                .HasMaxLength(2000)
                .IsRequired();

            entity.Property(c => c.IncidentLocation)
                .HasMaxLength(250)
                .IsRequired();

            entity.Property(c => c.IncidentDate)
                .IsRequired();

            entity.Property(c => c.ClaimedAmount)
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(c => c.CurrencyCode)
                .HasMaxLength(3)
                .IsRequired();

            entity.Property(c => c.MarketCode)
                .HasMaxLength(10)
                .IsRequired();

            entity.Property(c => c.Status)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(c => c.ReserveAmount)
                .HasPrecision(18, 2);

            entity.Property(c => c.ApprovedAmount)
                .HasPrecision(18, 2);

            entity.Property(c => c.DecisionReason)
                .HasMaxLength(1000);

            entity.Property(c => c.CreatedAt)
                .IsRequired();

            entity.Property(c => c.UpdatedAt)
                .IsRequired();

            // Optimistic concurrency: SQLite has no rowversion, so the service increments Version itself.
            // No HasDefaultValue: the CLR default (0) is sent explicitly on insert.
            entity.Property(c => c.Version)
                .IsConcurrencyToken();

            // Indexes for fast querying
            entity.HasIndex(c => c.ClaimNumber)
                .IsUnique();

            entity.HasIndex(c => c.Status);
            entity.HasIndex(c => c.MarketCode);
            entity.HasIndex(c => c.AssignedOfficerId);
            entity.HasIndex(c => c.CreatedAt);

            // Relationship 1: Claimant (Required 1-to-many)
            // One AppUser can submit many Claims.
            // OnDelete(DeleteBehavior.Restrict): deleting a user cannot cascade-delete active claims.
            entity.HasOne(c => c.Claimant)
                .WithMany(u => u.SubmittedClaims)
                .HasForeignKey(c => c.ClaimantId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);

            // Relationship 2: AssignedOfficer (Optional 1-to-many)
            // One AppUser can be assigned many Claims as an officer.
            // OnDelete(DeleteBehavior.SetNull): deleting an officer unassigns the claim, keeping the claim record intact.
            entity.HasOne(c => c.AssignedOfficer)
                .WithMany(u => u.AssignedClaims)
                .HasForeignKey(c => c.AssignedOfficerId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            // Relationship 3: Activities (1-to-many)
            // One Claim has many ClaimActivity timeline entries.
            // OnDelete(DeleteBehavior.Restrict): protects audit records from accidental cascading deletion.
            entity.HasMany(c => c.Activities)
                .WithOne(a => a.Claim)
                .HasForeignKey(a => a.ClaimId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ==========================================
        // ClaimActivity Configuration
        // ==========================================
        modelBuilder.Entity<ClaimActivity>(entity =>
        {
            entity.HasKey(a => a.Id);

            entity.Property(a => a.ActivityType)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(a => a.Description)
                .HasMaxLength(1000)
                .IsRequired();

            entity.Property(a => a.CreatedAt)
                .IsRequired();

            // Indexes
            entity.HasIndex(a => a.ClaimId);
            entity.HasIndex(a => a.CreatedAt);

            // Relationship 4: ActorUser (Optional reference)
            // Refers to AppUser who took the action (null indicates system-generated).
            // OnDelete(DeleteBehavior.SetNull): preserves the audit record if the acting user account is deleted.
            entity.HasOne(a => a.ActorUser)
                .WithMany()
                .HasForeignKey(a => a.ActorUserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ==========================================
        // InformationRequest Configuration
        // ==========================================
        modelBuilder.Entity<InformationRequest>(entity =>
        {
            entity.HasKey(r => r.Id);

            entity.Property(r => r.Question)
                .HasMaxLength(1000)
                .IsRequired();

            entity.Property(r => r.Response)
                .HasMaxLength(2000);

            entity.Property(r => r.CreatedAt)
                .IsRequired();

            // IsAnswered is derived from RespondedAt, so it is not stored.
            entity.Ignore(r => r.IsAnswered);

            entity.HasIndex(r => r.ClaimId);

            entity.HasOne(r => r.Claim)
                .WithMany(c => c.InformationRequests)
                .HasForeignKey(r => r.ClaimId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.RequestedBy)
                .WithMany()
                .HasForeignKey(r => r.RequestedByUserId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
