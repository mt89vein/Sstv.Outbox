using System.Diagnostics;
using System.Text;
using Confluent.Kafka;

namespace Sstv.Outbox.Kafka;

/// <summary>
/// Publisher to kafka.
/// </summary>
public sealed class OutboxKafkaHandler<TOutboxItem> : IOutboxItemHandler<TOutboxItem>
    where TOutboxItem : class, IKafkaOutboxItem
{
    // private readonly IProducer<byte[]?, byte[]?> _producer;
    //
    // /// <summary>
    // /// Creates new instance of <see cref="OutboxKafkaHandler{TOutboxItem}"/>.
    // /// </summary>
    // /// <param name="producer">Producer.</param>
    // public OutboxKafkaHandler(IProducer<byte[]?, byte[]?> producer)
    // {
    //     _producer = producer;
    // }

    /// <summary>
    /// Processes outbox item.
    /// </summary>
    /// <param name="outboxItem">Outbox item.</param>
    /// <param name="options">Outbox options.</param>
    /// <param name="ct">Token for cancelling operation.</param>
    public async Task<OutboxItemHandleResult> HandleAsync(
        TOutboxItem outboxItem,
        OutboxOptions options,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(outboxItem);
        ArgumentNullException.ThrowIfNull(options);

        using var activity = StartTracingActivity(outboxItem.Topic ?? "N/A", outboxItem.Headers);

        Headers? headers = null;

        if (outboxItem.Headers is not null)
        {
            headers = [];

            foreach (var messageHeader in outboxItem.Headers)
            {
                headers.Add(messageHeader.Key, Encoding.UTF8.GetBytes(messageHeader.Value));
            }
        }

        var message = new Message<byte[]?, byte[]?>
        {
            Key = outboxItem.Key,
            Value = outboxItem.Value,
            Headers = headers!,
            Timestamp = outboxItem.Timestamp.HasValue
                ? new Timestamp(outboxItem.Timestamp.Value)
                : Timestamp.Default
        };

        var config = options.GetKafkaTopicConfig();
        var result = await config.Producer.ProduceAsync(outboxItem.Topic ?? config.DefaultTopicName, message, ct);

        return result.Status switch
        {
            PersistenceStatus.Persisted => OutboxItemHandleResult.Ok,
            PersistenceStatus.NotPersisted => OutboxItemHandleResult.Retry,
            PersistenceStatus.PossiblyPersisted or _ => OutboxItemHandleResult.Retry
        };
    }

    private static Activity? StartTracingActivity(string topic, IReadOnlyDictionary<string, string>? headers)
    {
        var tracingHeaderValue = headers?.GetValueOrDefault("traceparent");

        var activity = ActivitySources.Tracing.CreateActivity($"{topic} send from outbox",
            ActivityKind.Producer, tracingHeaderValue);

        if (activity is null)
        {
            return activity;
        }

        return activity
            .SetTag("outbox.message.topic", topic)
            .Start();
    }
}
