using Confluent.Kafka;

namespace Sstv.Outbox.Kafka;

/// <summary>
/// Topic config.
/// </summary>
public class KafkaTopicConfig
{
    /// <summary>
    /// Default topic name.
    /// </summary>
    public string? DefaultTopicName { get; set; }

    /// <summary>
    /// Producer.
    /// </summary>
    public required IProducer<byte[]?, byte[]?> Producer { get; set; }
}

/// <summary>
/// Outbox kafka publisher configuration.
/// </summary>
public sealed class KafkaTopicConfig<TKey, TValue> : KafkaTopicConfig
{
    /// <summary>
    /// Kafka message Key serializer.
    /// </summary>
    public required ISerializer<TKey> KeySerializer { get; set; }

    /// <summary>
    /// Kafka message Key deserializer.
    /// </summary>
    public required IDeserializer<TKey> KeyDeserializer { get; set; }

    /// <summary>
    /// Kafka message Value deserializer.
    /// </summary>
    public required IDeserializer<TValue> ValueDeserializer { get; set; }

    /// <summary>
    /// Kafka message Value serializer.
    /// </summary>
    public required ISerializer<TValue> ValueSerializer { get; set; }
}
