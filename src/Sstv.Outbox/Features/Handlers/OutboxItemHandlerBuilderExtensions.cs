using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Sstv.Outbox;

/// <summary>
/// Outbox item handler builder extensions.
/// </summary>
public static class OutboxItemHandlerBuilderExtensions
{
    /// <summary>
    /// Adds outbox item handler.
    /// </summary>
    /// <param name="builder">Outbox item handler builder.</param>
    /// <typeparam name="TOutboxItemHandler">Type of outbox item handler.</typeparam>
    /// <typeparam name="TOutboxItem">Type of outbox item.</typeparam>
    public static OutboxItemHandlerBuilder<TOutboxItem> WithHandler<TOutboxItem, TOutboxItemHandler>(
        this OutboxItemHandlerBuilder<TOutboxItem> builder
    )
        where TOutboxItem : class, IOutboxItem
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

        builder.HandlerType = typeof(TOutboxItemHandler);

        return builder;
    }

    /// <summary>
    /// Adds outbox item batch handler.
    /// </summary>
    /// <param name="builder">Outbox item handler builder.</param>
    /// <typeparam name="TOutboxItemBatchHandler">Type of outbox item batch handler.</typeparam>
    /// <typeparam name="TOutboxItem">Type of outbox item.</typeparam>
    public static OutboxItemHandlerBuilder<TOutboxItem> WithBatchHandler<TOutboxItem, TOutboxItemBatchHandler>(
        this OutboxItemHandlerBuilder<TOutboxItem> builder
    )
        where TOutboxItem : class, IOutboxItem, new()
        where TOutboxItemBatchHandler : class, IOutboxItemBatchHandler<TOutboxItem>
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (builder.HandlerType is not null)
        {
            throw new NotSupportedException(
                $"You are trying to register {typeof(TOutboxItemBatchHandler)} as a second outbox item handler for {typeof(TOutboxItem)}, which is not supported." +
                $" Remove previous handler first {builder.HandlerType}."
            );
        }

        builder.Services.TryAddTransient<IOutboxItemBatchHandler<TOutboxItem>, TOutboxItemBatchHandler>();

        builder.HandlerType = typeof(TOutboxItemBatchHandler);

        return builder;
    }
}
