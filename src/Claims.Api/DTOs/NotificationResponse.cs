using Claims.Api.Models;

namespace Claims.Api.DTOs;

public record NotificationResponse(Guid Id, Guid ClaimId, string Message, DateTimeOffset CreatedAt)
{
    public static NotificationResponse From(Notification n) => new(n.Id, n.ClaimId, n.Message, n.CreatedAt);
}
