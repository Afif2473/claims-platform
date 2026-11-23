using System.Text.Json;
using Claims.Api.Interfaces;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace Claims.Api.Messaging;

/// <summary>
/// Publishes claim events to Kafka. Singleton: one producer (and its connection) for the whole app.
///
/// Message key = ClaimId. Kafka keeps messages with the same key in one partition, so all events
/// of one claim are consumed in the order they happened.
///
/// Produce() is non-blocking: the message is queued and sent in the background. A delivery failure is
/// logged but never surfaces to the user, because the claim change is already committed.
/// Known gap: an event can be lost if delivery fails after the commit. The production fix is
/// the transactional outbox pattern (out of scope, see docs/task-09-kafka-events.md).
/// </summary>
public sealed class KafkaClaimEventPublisher : IClaimEventPublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly string _topic;
    private readonly ILogger<KafkaClaimEventPublisher> _logger;

    public KafkaClaimEventPublisher(IOptions<KafkaOptions> options, ILogger<KafkaClaimEventPublisher> logger)
    {
        _topic = options.Value.Topic;
        _logger = logger;
        _producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = options.Value.BootstrapServers,
            Acks = Acks.All,              // wait for the broker to persist the message
            EnableIdempotence = true,     // broker-side de-duplication of producer retries
            MessageTimeoutMs = 10_000     // give up (and log) after 10s instead of the 5-minute default
        })
        .SetErrorHandler((_, error) => KafkaLogging.Log(_logger, "producer", error))
        .Build();
    }

    public void Publish(ClaimEvent claimEvent)
    {
        var message = new Message<string, string>
        {
            Key = claimEvent.ClaimId.ToString(),
            Value = JsonSerializer.Serialize(claimEvent, ClaimEventJson.Options)
        };

        try
        {
            _producer.Produce(_topic, message, report =>
            {
                if (report.Error.IsError)
                    _logger.LogError("Failed to publish {EventType} {EventId} for claim {ClaimNumber}: {Reason}",
                        claimEvent.EventType, claimEvent.EventId, claimEvent.ClaimNumber, report.Error.Reason);
            });
        }
        catch (ProduceException<string, string> ex)
        {
            // e.g. local queue full. The claim change is already saved, so log and move on.
            _logger.LogError(ex, "Failed to queue {EventType} {EventId} for claim {ClaimNumber}",
                claimEvent.EventType, claimEvent.EventId, claimEvent.ClaimNumber);
        }
    }

    public void Dispose()
    {
        // Give queued messages a chance to be delivered on shutdown.
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }
}
