using Claims.Api.Data;
using Claims.Api.Interfaces;
using Claims.Api.Messaging;
using Claims.Api.Models;
using Claims.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Claims.Api.Tests.Support;

/// <summary>
/// A real SQLite database in memory, kept alive by one open connection for the duration of a test.
/// Chosen over EF's InMemory provider because InMemory enforces neither concurrency tokens nor
/// foreign keys, which are exactly the behaviours these tests need to prove.
/// The schema is built by running the real migrations, so the migrations are tested too.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public RecordingEventPublisher Events { get; } = new();

    public TestDatabase()
    {
        _connection.Open();
        using var db = CreateContext();
        db.Database.Migrate();
        DbSeeder.SeedAsync(db).GetAwaiter().GetResult();
    }

    public ClaimsDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ClaimsDbContext>().UseSqlite(_connection).Options);

    /// <summary>
    /// Services wired the way one HTTP request would get them: a fresh DbContext, acting as the given user.
    /// </summary>
    public (ClaimService Claims, ClaimWorkflowService Workflow) ServicesFor(Guid userId)
    {
        var db = CreateContext();
        var currentUser = new FixedCurrentUser(db.Users.AsNoTracking().Single(u => u.Id == userId));
        var claims = new ClaimService(db, currentUser, Events);
        return (claims, new ClaimWorkflowService(db, currentUser, claims, Events));
    }

    public void Dispose() => _connection.Dispose();
}

public sealed class FixedCurrentUser(AppUser user) : ICurrentUserService
{
    public Task<AppUser> GetRequiredUserAsync(CancellationToken cancellationToken = default) => Task.FromResult(user);
}

/// <summary>Captures what would have been sent to Kafka.</summary>
public sealed class RecordingEventPublisher : IClaimEventPublisher
{
    public List<ClaimEvent> Published { get; } = [];
    public void Publish(ClaimEvent claimEvent) => Published.Add(claimEvent);
}
