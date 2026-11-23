using Claims.Api.Data;
using Claims.Api.DTOs;
using Claims.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Claims.Api.Controllers;

[ApiController]
[Route("api/users")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly ClaimsDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UsersController(ClaimsDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    /// <summary>
    /// DEMO ONLY: lists active users so the frontend can offer a "sign in as" switcher.
    /// It would not exist once real authentication replaces the X-User-Id header.
    /// </summary>
    [HttpGet]
    public async Task<IReadOnlyList<UserResponse>> List(CancellationToken cancellationToken)
    {
        var users = await _db.Users.AsNoTracking()
            .Where(u => u.IsActive)
            .OrderBy(u => u.Role).ThenBy(u => u.FullName)
            .ToListAsync(cancellationToken);
        return users.Select(UserResponse.From).ToList();
    }

    /// <summary>The user identified by the X-User-Id header.</summary>
    [HttpGet("me")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<UserResponse> Me(CancellationToken cancellationToken)
        => UserResponse.From(await _currentUser.GetRequiredUserAsync(cancellationToken));
}
