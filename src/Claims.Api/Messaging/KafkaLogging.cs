using Confluent.Kafka;

namespace Claims.Api.Messaging;

/// <summary>
/// librdkafka reports every failed reconnect attempt as an error, even though it retries them
/// itself. With the broker down that came to about 170 log lines in 13 seconds. Only the
/// signals someone should act on are surfaced; the rest stay at Debug.
/// </summary>
public static class KafkaLogging
{
    public static void Log(ILogger logger, string client, Error error)
    {
        if (error.IsFatal)
            logger.LogError("Kafka {Client} fatal error: {Reason}", client, error.Reason);
        else if (error.Code == ErrorCode.Local_AllBrokersDown)
            logger.LogWarning("Kafka {Client}: all brokers are down; retrying in the background", client);
        else
            logger.LogDebug("Kafka {Client} transient error: {Reason}", client, error.Reason);
    }
}
