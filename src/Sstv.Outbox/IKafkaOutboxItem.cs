namespace Sstv.Outbox;

/// <summary>
/// Outbox item that publishes to kafka topic.
/// </summary>
public interface IKafkaOutboxItem : IOutboxItem
{
    /// <summary>
    /// Headers.
    /// </summary>
    public Dictionary<string, string>? Headers { get; set; }

    /// <summary>
    /// Key.
    /// </summary>
    public byte[]? Key { get; set; }

    /// <summary>
    /// Message
    /// </summary>
    public byte[]? Value { get; set; }

    /// <summary>
    /// Kafka topic.
    /// </summary>
    public string? Topic { get; set; }

    /// <summary>
    /// Timestamp of event/message.
    /// </summary>
    public DateTimeOffset? Timestamp { get; set; }
}
