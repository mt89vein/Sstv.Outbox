using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Sstv.Outbox.Kafka;

/// <summary>
/// Outbox item handler builder extensions.
/// </summary>
public static class OutboxItemHandlerBuilderExtensions
{
    /// <summary>
    /// Adds kafka handler as an outbox handler.
    /// </summary>
    /// <param name="builder">Outbox item handler builder.</param>
    /// <param name="config"></param>
    /// <typeparam name="TOutboxItemHandler">Type of outbox item handler.</typeparam>
    /// <typeparam name="TOutboxItem">Type of outbox item.</typeparam>
    /// <typeparam name="TKey">Type of kafka message key.</typeparam>
    /// <typeparam name="TValue">Type of kafka message value.</typeparam>
    public static OutboxItemHandlerBuilder<TOutboxItem> WithKafkaProducer<TOutboxItemHandler, TOutboxItem, TKey, TValue>(
        this OutboxItemHandlerBuilder<TOutboxItem> builder,
        KafkaTopicConfig<TKey, TValue> config
    )
        where TOutboxItem : class, IKafkaOutboxItem, new()
        where TOutboxItemHandler : class, IOutboxItemHandler<TOutboxItem>
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (builder.HandlerType is not null)
        {
            throw new NotSupportedException(
                $"You are trying to register {typeof(TOutboxItemHandler)} as a second outbox item handler for {typeof(TOutboxItem)}, which is not supported." +
                $" Remove previous handler first {builder.HandlerType}."
            );
        }

        builder.Services.TryAddTransient<IOutboxItemHandler<TOutboxItem>, TOutboxItemHandler>();
        builder.Services.TryAddSingleton<IKafkaOutboxItemFactory<TOutboxItem>, KafkaOutboxItemFactory<TOutboxItem>>();
        builder.Services.Configure<OutboxOptions>(builder.OutboxName, o =>
        {
            o.ConfigureKafka(config);
        });

        builder.HandlerType = typeof(TOutboxItemHandler);

        return builder;
    }

    /// <summary>
    /// Adds kafka handler as an outbox handler.
    /// </summary>
    /// <param name="builder">Outbox item handler builder.</param>
    /// <param name="config"></param>
    /// <typeparam name="TOutboxItemHandler">Type of outbox item handler.</typeparam>
    /// <typeparam name="TOutboxItem">Type of outbox item.</typeparam>
    /// <typeparam name="TKey">Type of kafka message key.</typeparam>
    /// <typeparam name="TValue">Type of kafka message value.</typeparam>
    public static OutboxItemHandlerBuilder<TOutboxItem> WithBatchKafkaProducer<TOutboxItemHandler, TOutboxItem, TKey, TValue>(
        this OutboxItemHandlerBuilder<TOutboxItem> builder,
        KafkaTopicConfig<TKey, TValue> config
    )
        where TOutboxItem : class, IKafkaOutboxItem, new()
        where TOutboxItemHandler : class, IOutboxItemBatchHandler<TOutboxItem>
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (builder.HandlerType is not null)
        {
            throw new NotSupportedException(
                $"You are trying to register {typeof(TOutboxItemHandler)} as a second outbox item handler for {typeof(TOutboxItem)}, which is not supported." +
                $" Remove previous handler first {builder.HandlerType}."
            );
        }

        builder.Services.TryAddTransient<IOutboxItemBatchHandler<TOutboxItem>, TOutboxItemHandler>();
        builder.Services.TryAddSingleton<IKafkaOutboxItemFactory<TOutboxItem>, KafkaOutboxItemFactory<TOutboxItem>>();
        builder.Services.Configure<OutboxOptions>(builder.OutboxName, o =>
        {
            o.ConfigureKafka(config);
        });

        builder.HandlerType = typeof(TOutboxItemHandler);

        return builder;
    }
}
