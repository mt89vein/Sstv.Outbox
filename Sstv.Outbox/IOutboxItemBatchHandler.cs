namespace Sstv.Outbox;

/// <summary>
/// Batch handler for OutboxItems.
/// </summary>
/// <typeparam name="TOutboxItem">OutboxItem db entity type. Must implement <see cref="IOutboxItem"/> interface.</typeparam>
public interface IOutboxItemBatchHandler<in TOutboxItem>
    where TOutboxItem : class, IOutboxItem
{
    /// <summary>
    /// Processes outbox items in batch.
    /// </summary>
    /// <param name="items">Outbox items.</param>
    /// <param name="options">Outbox options.</param>
    /// <param name="ct">Token for cancelling operation.</param>
    Task<IReadOnlyDictionary<Guid, OutboxItemHandleResult>> HandleAsync(
        IReadOnlyCollection<TOutboxItem> items,
        OutboxOptions options,
        CancellationToken ct = default
    );
}