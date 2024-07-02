namespace Sstv.Outbox;

public sealed class MyOutboxItem : IOutboxItem, IHasStatus
{
    /// <summary>
    /// Unique identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Created at timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Status.
    /// </summary>
    public OutboxItemStatus Status { get; set; }

    /// <summary>
    /// How many times retried already.
    /// </summary>
    public int? RetryCount { get; set; }

    /// <summary>
    /// When to retry.
    /// </summary>
    public DateTimeOffset? RetryAfter { get; set; }

    /// <summary>
    /// Headers.
    /// </summary>
    public byte[]? Headers { get; set; }

    /// <summary>
    /// Data.
    /// </summary>
    public byte[]? Data { get; set; }
}

/// <summary>
/// Batch handler for MyOutboxItem.
/// </summary>
public sealed class MyOutboxItemHandler : IOutboxItemBatchHandler<MyOutboxItem>
{
    /// <summary>
    /// Processes outbox items in batch.
    /// </summary>
    /// <param name="items">Outbox items.</param>
    /// <param name="options">Outbox options.</param>
    /// <param name="ct">Token for cancelling operation.</param>
    public Task<IReadOnlyDictionary<Guid, OutboxItemHandleResult>> HandleAsync(
        IReadOnlyCollection<MyOutboxItem> items,
        OutboxOptions options,
        CancellationToken ct
    )
    {
        ArgumentNullException.ThrowIfNull(items);

        var result = new Dictionary<Guid, OutboxItemHandleResult>(items.Count);

        foreach (var item in items)
        {
            Console.WriteLine("MyOutboxItem Handled {0} retry number: {1}", item.Id.ToString(), item.RetryCount.GetValueOrDefault(0).ToString());

            result.Add(item.Id, (OutboxItemHandleResult)Random.Shared.Next(1, 3));
        }

        return Task.FromResult<IReadOnlyDictionary<Guid, OutboxItemHandleResult>>(result);
    }
}