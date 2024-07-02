namespace Sstv.Outbox;

/// <summary>
/// Handler for OutboxItem.
/// </summary>
/// <typeparam name="TOutboxItem">OutboxItem db entity type. Must implement <see cref="IOutboxItem"/> interface.</typeparam>
public interface IOutboxItemHandler<in TOutboxItem>
    where TOutboxItem : class, IOutboxItem
{
    /// <summary>
    /// Processes outbox item.
    /// </summary>
    /// <param name="item">Outbox item.</param>
    /// <param name="options">Outbox options.</param>
    /// <param name="ct">Token for cancelling operation.</param>
    Task<OutboxItemHandleResult> HandleAsync(
        TOutboxItem item,
        OutboxOptions options,
        CancellationToken ct = default
    );
}