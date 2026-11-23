using Claims.Api.Messaging;

namespace Claims.Api.Interfaces;

/// <summary>
/// Publishes claim events after the change has been committed.
/// Fire-and-forget by design: publishing never blocks or fails the user's request.
/// </summary>
public interface IClaimEventPublisher
{
    void Publish(ClaimEvent claimEvent);
}
