using Claims.Api.Data;
using Claims.Api.DTOs;
using Claims.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Claims.Api.Controllers;

/// <summary>
/// Read side of the Kafka flow. Notifications are written asynchronously by ClaimEventConsumer,
/// so one may appear a moment after the action that caused it (eventual consistency).
/// </summary>
[ApiController]
[Route("api/notifications")]
[Produces("application/json")]
public class NotificationsController : ControllerBase
{
    private readonly ClaimsDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public NotificationsController(ClaimsDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    /// <summary>The current user's notifications, newest first.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<NotificationResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IReadOnlyList<NotificationResponse>> List(CancellationToken cancellationToken)
    {
        var user = await _currentUser.GetRequiredUserAsync(cancellationToken);
        var notifications = await _db.Notifications.AsNoTracking()
            .Where(n => n.UserId == user.Id)
            .ToListAsync(cancellationToken);

        // Sorted in memory: SQLite cannot ORDER BY DateTimeOffset (see task-06 doc).
        return notifications
            .OrderByDescending(n => n.CreatedAt)
            .Select(NotificationResponse.From)
            .ToList();
    }
}
