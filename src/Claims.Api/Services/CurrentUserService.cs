using Claims.Api.Data;
using Claims.Api.Errors;
using Claims.Api.Interfaces;
using Claims.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Claims.Api.Services;

/// <summary>
/// SIMULATED IDENTITY for the assessment: the caller identifies itself with an X-User-Id header
/// holding a seeded user's id. This is not authentication. Anyone can claim to be anyone.
/// In production this would read the subject claim of a validated JWT (e.g. Azure AD / OIDC).
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    public const string HeaderName = "X-User-Id";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ClaimsDbContext _db;
    private AppUser? _user;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor, ClaimsDbContext db)
    {
        _httpContextAccessor = httpContextAccessor;
        _db = db;
    }

    public async Task<AppUser> GetRequiredUserAsync(CancellationToken cancellationToken = default)
    {
        // Scoped service: resolve once per request.
        if (_user is not null)
            return _user;

        var headerValue = _httpContextAccessor.HttpContext?.Request.Headers[HeaderName].ToString();
        if (!Guid.TryParse(headerValue, out var userId))
            throw new UnauthenticatedException($"Missing or invalid {HeaderName} header.");

        _user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, cancellationToken)
            ?? throw new UnauthenticatedException("Unknown or inactive user.");

        return _user;
    }
}
