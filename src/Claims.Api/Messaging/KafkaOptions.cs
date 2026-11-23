namespace Claims.Api.Messaging;

/// <summary>Bound from the "Kafka" section of appsettings.json.</summary>
public class KafkaOptions
{
    public const string SectionName = "Kafka";

    /// <summary>When false, events are not published and the consumer does not start (run without Docker).</summary>
    public bool Enabled { get; set; } = true;

    public string BootstrapServers { get; set; } = "localhost:9092";
    public string Topic { get; set; } = "claim-events";
    public string ConsumerGroup { get; set; } = "claims-notifications";
}
