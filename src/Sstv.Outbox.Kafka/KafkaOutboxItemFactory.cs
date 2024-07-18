using System.Diagnostics;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace Sstv.Outbox.Kafka;

/// <summary>
/// Factory of <typeparamref name="TOutboxItem"/>.
/// </summary>
/// <typeparam name="TOutboxItem">OutboxItem type.</typeparam>
public sealed class KafkaOutboxItemFactory<TOutboxItem> : IKafkaOutboxItemFactory<TOutboxItem>
    where TOutboxItem : class, IKafkaOutboxItem, new()
{
    /// <summary>
    /// Outbox options.
    /// </summary>
    private readonly OutboxOptions _options;

    /// <summary>
    /// Creates new instance of <see cref="KafkaOutboxItemFactory{TOutboxItem}"/>.
    /// </summary>
    /// <param name="optionsMonitor">Outbox options. accessor.</param>
    public KafkaOutboxItemFactory(IOptionsMonitor<OutboxOptions> optionsMonitor)
    {
        ArgumentNullException.ThrowIfNull(optionsMonitor);

        _options = optionsMonitor.Get(typeof(TOutboxItem).Name);
    }

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
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        var item = Create(key, value, additionalHeaders);

        item.Topic = topic;

        return item;
    }

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
    )
    {
        var headers = Activity.Current?.Context.GetHeaders() ??
                      new Dictionary<string, string>();

        if (additionalHeaders is not null)
        {
            foreach (var header in additionalHeaders)
            {
                headers.Add(header.Key, header.Value);
            }
        }

        var config = _options.GetKafkaConfig<TKey, TValue>();

        return new TOutboxItem
        {
            Id = _options.NextGuid(),
            Key = key is not null ? config.KeySerializer.Serialize(key, SerializationContext.Empty) : null,
            Value = value is not null ? config.ValueSerializer.Serialize(value, SerializationContext.Empty) : null,
            Headers = headers,
            Timestamp = _options.CurrentTime()
        };
    }
}
