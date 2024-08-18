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
    Task<OutboxBatchResult> HandleAsync(
        IReadOnlyCollection<TOutboxItem> items,
        OutboxOptions options,
        CancellationToken ct = default
    );
}

/// <summary>
/// Result of process outbox items batch.
/// </summary>
public readonly record struct OutboxBatchResult
{
    /// <summary>
    /// Is batch fully processed.
    /// </summary>
    internal bool AllProcessed { get; init; }

    /// <summary>
    /// Process info.
    /// </summary>
    internal IReadOnlyDictionary<Guid, OutboxItemHandleResult> ProcessedInfo { get; init; }

    /// <summary>
    /// Is batch fully processed.
    /// </summary>
    public static OutboxBatchResult FullyProcessed { get; } = new()
    {
        AllProcessed = true,
        ProcessedInfo = new Dictionary<Guid, OutboxItemHandleResult>()
    };

    /// <summary>
    /// Batch not fully proccessed result.
    /// </summary>
    /// <param name="processedInfo">Processed info.</param>
    public static OutboxBatchResult ProcessedPartially(IReadOnlyDictionary<Guid, OutboxItemHandleResult> processedInfo)
    {
        return new OutboxBatchResult
        {
            AllProcessed = false,
            ProcessedInfo = processedInfo,
        };
    }
}
