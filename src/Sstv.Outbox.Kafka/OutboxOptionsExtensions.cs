namespace Sstv.Outbox.Kafka;

/// <summary>
/// Extensions for <see cref="OutboxOptions"/>.
/// </summary>
internal static class OutboxOptionsExtensions
{
    /// <summary>
    /// The key, where kafka topic configuration stored in metadata.
    /// </summary>
    private const string KafkaTopicConfigKey = "KafkaTopicConfig";

    /// <summary>
    /// Configure kafka.
    /// </summary>
    /// <param name="options">Outbox options.</param>
    /// <param name="config">Kafka config.</param>
    public static void ConfigureKafka<TKey, TValue>(
        this OutboxOptions options,
        KafkaTopicConfig<TKey, TValue> config
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(config);

        options.Set(KafkaTopicConfigKey, config);
    }

    /// <summary>
    /// Returns <see cref="KafkaTopicConfigKey"/> from metadata.
    /// </summary>
    /// <param name="options">Outbox options.</param>
    /// <returns>Kafka config.</returns>
    public static KafkaTopicConfig<TKey, TValue> GetKafkaConfig<TKey, TValue>(this OutboxOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Get<KafkaTopicConfig<TKey, TValue>>(KafkaTopicConfigKey);
    }

    /// <summary>
    /// Returns <see cref="KafkaTopicConfigKey"/> from metadata.
    /// </summary>
    /// <param name="options">Outbox options.</param>
    /// <returns>Kafka config.</returns>
    public static KafkaTopicConfig GetKafkaTopicConfig(this OutboxOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Get<KafkaTopicConfig>(KafkaTopicConfigKey);
    }
}
