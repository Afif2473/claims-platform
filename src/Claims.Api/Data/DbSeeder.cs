using Claims.Api.Enums;
using Claims.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Claims.Api.Data;

/// <summary>
/// Seeds demo users with fixed ids so testers (and the Angular user switcher) can use them
/// as X-User-Id values. Runs only when the Users table is empty.
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
        if (await db.Users.AnyAsync(cancellationToken))
            return;

        db.Users.AddRange(
            new AppUser { Id = JaneTanId, FullName = "Jane Tan", Email = "jane.tan@example.com", Role = UserRole.Claimant },
            new AppUser { Id = ArjunKumarId, FullName = "Arjun Kumar", Email = "arjun.kumar@example.com", Role = UserRole.Claimant },
            new AppUser { Id = DavidWongId, FullName = "David Wong", Email = "david.wong@example.com", Role = UserRole.ClaimsOfficer },
            new AppUser { Id = MeiLinId, FullName = "Mei Lin", Email = "mei.lin@example.com", Role = UserRole.ClaimsOfficer },
            new AppUser { Id = SarahLimId, FullName = "Sarah Lim", Email = "sarah.lim@example.com", Role = UserRole.Manager });

        await db.SaveChangesAsync(cancellationToken);
    }
}
