using Claims.Api.Models;

namespace Claims.Api.Interfaces;

/// <summary>
/// Resolves who is making the current request. Services depend on this abstraction,
/// so swapping the simulated X-User-Id header for real JWT auth only changes the implementation.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>Returns the active user making the request, or throws UnauthenticatedException.</summary>
    Task<AppUser> GetRequiredUserAsync(CancellationToken cancellationToken = default);
}
