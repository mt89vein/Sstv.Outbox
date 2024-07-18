namespace Sstv.Outbox.Kafka;

/// <summary>
/// Factory of <typeparamref name="TOutboxItem"/>.
/// </summary>
/// <typeparam name="TOutboxItem">OutboxItem type.</typeparam>
public interface IKafkaOutboxItemFactory<out TOutboxItem>
    where TOutboxItem : class, IKafkaOutboxItem
{
    /// <summary>
    /// Creates <typeparamref name="TOutboxItem"/>.
    /// </summary>
    /// <param name="key">Kafka message key.</param>
    /// <param name="value">Kafka message value.</param>
    /// <param name="topic">Topic name.</param>
    /// <param name="additionalHeaders">Kafka message headers.</param>
    /// <typeparam name="TKey">Type of Key.</typeparam>
    /// <typeparam name="TValue">Type of Value.</typeparam>
    /// <returns>Created item.</returns>
    public TOutboxItem Create<TKey, TValue>(
        TKey? key,
        TValue? value,
        string topic,
        IDictionary<string, string>? additionalHeaders = null
    );

    /// <summary>
    /// Creates <typeparamref name="TOutboxItem"/>.
    /// </summary>
    /// <param name="key">Kafka message key.</param>
    /// <param name="value">Kafka message value.</param>
    /// <param name="additionalHeaders">Kafka message headers.</param>
    /// <typeparam name="TKey">Type of Key.</typeparam>
    /// <typeparam name="TValue">Type of Value.</typeparam>
    /// <returns>Created item.</returns>
    public TOutboxItem Create<TKey, TValue>(
        TKey? key,
        TValue? value,
        IDictionary<string, string>? additionalHeaders = null
    );
}
