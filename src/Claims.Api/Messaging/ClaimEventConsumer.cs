using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Options;

namespace Claims.Api.Messaging;

/// <summary>
/// Background service that consumes claim-events and creates claimant notifications.
///
/// Delivery semantics: at-least-once. Auto-commit is off and the offset is committed only after the
/// notification is saved. A crash before the commit means the message is redelivered, and the unique
/// EventId on Notification turns that redelivery into a no-op.
///
/// It runs in the API process for simplicity. Because it only talks to the API through Kafka and the
/// database, it could be moved into its own worker service without code changes.
/// </summary>
public sealed class ClaimEventConsumer : BackgroundService
{
    private readonly KafkaOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ClaimEventConsumer> _logger;

    public ClaimEventConsumer(IOptions<KafkaOptions> options, IServiceScopeFactory scopeFactory, ILogger<ClaimEventConsumer> logger)
    {
        _options = options.Value;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    // Confluent's Consume() is blocking, so the loop runs on its own thread and does not hold up app startup.
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.Run(() => RunAsync(stoppingToken), stoppingToken);

    private async Task RunAsync(CancellationToken stoppingToken)
    {
        await EnsureTopicExistsAsync();

        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = _options.ConsumerGroup,
            AutoOffsetReset = AutoOffsetReset.Earliest, // a new consumer group processes existing events
            EnableAutoCommit = false                    // commit only after the notification is saved
        })
        .SetErrorHandler((_, error) => KafkaLogging.Log(_logger, "consumer", error))
        .Build();

        consumer.Subscribe(_options.Topic);
        _logger.LogInformation("Consuming {Topic} as group {Group}", _options.Topic, _options.ConsumerGroup);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string>? result = null;
                try
                {
                    result = consumer.Consume(stoppingToken);
                    var claimEvent = JsonSerializer.Deserialize<ClaimEvent>(result.Message.Value, ClaimEventJson.Options)
                        ?? throw new JsonException("Empty claim event.");

                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var handler = scope.ServiceProvider.GetRequiredService<ClaimNotificationHandler>();
                        await handler.HandleAsync(claimEvent, stoppingToken);
                    }

                    consumer.Commit(result);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (JsonException ex) when (result is not null)
                {
                    // A malformed message will never succeed. Log it and skip it so it can't block the partition.
                    // (Production: send it to a dead-letter topic.)
                    _logger.LogError(ex, "Skipping malformed message at {TopicPartitionOffset}", result.TopicPartitionOffset);
                    consumer.Commit(result);
                }
                catch (Exception ex)
                {
                    // Not committed. Rewind to the failed message so it is retried after a short pause.
                    _logger.LogError(ex, "Failed to process claim event; will retry");
                    if (result is not null)
                        consumer.Seek(result.TopicPartitionOffset);
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
        finally
        {
            consumer.Close(); // leave the group cleanly so partitions are reassigned quickly
        }
    }

    /// <summary>Creates the topic on first run so the consumer does not start against a missing topic.</summary>
    private async Task EnsureTopicExistsAsync()
    {
        using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = _options.BootstrapServers }).Build();
        try
        {
            await admin.CreateTopicsAsync([new TopicSpecification { Name = _options.Topic, NumPartitions = 3, ReplicationFactor = 1 }]);
            _logger.LogInformation("Created Kafka topic {Topic}", _options.Topic);
        }
        catch (CreateTopicsException ex) when (ex.Results.All(r => r.Error.Code == ErrorCode.TopicAlreadyExists))
        {
            // Already there.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not ensure Kafka topic {Topic} exists (is the broker running?)", _options.Topic);
        }
    }
}
