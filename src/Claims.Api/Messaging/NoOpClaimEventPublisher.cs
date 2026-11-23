using Claims.Api.Interfaces;

namespace Claims.Api.Messaging;

/// <summary>Used when Kafka:Enabled is false, so the API runs without a broker. Events are only logged.</summary>
public sealed class NoOpClaimEventPublisher : IClaimEventPublisher
{
    private readonly ILogger<NoOpClaimEventPublisher> _logger;

    public NoOpClaimEventPublisher(ILogger<NoOpClaimEventPublisher> logger)
    {
        _logger = logger;
    }

    public void Publish(ClaimEvent claimEvent) =>
        _logger.LogDebug("Kafka disabled; not publishing {EventType} for claim {ClaimNumber}", claimEvent.EventType, claimEvent.ClaimNumber);
}
