namespace Sstv.Outbox;

public sealed class OneMoreOutboxItem : IOutboxItem, IHasStatus
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
/// Handler for OneMoreOutboxItem.
/// </summary>
public sealed class OneMoreOutboxItemHandler : IOutboxItemHandler<OneMoreOutboxItem>
{
    /// <summary>
    /// Processes outbox item.
    /// </summary>
    /// <param name="item">Outbox item.</param>
    /// <param name="options">Outbox options.</param>
    /// <param name="ct">Token for cancelling operation.</param>
    public Task<OutboxItemHandleResult> HandleAsync(
        OneMoreOutboxItem item,
        OutboxOptions options,
        CancellationToken ct
    )
    {
        ArgumentNullException.ThrowIfNull(item);

        Console.WriteLine("OneMoreOutboxItem Handled {0} retry number: {1}", item.Id.ToString(),
            item.RetryCount.GetValueOrDefault(0).ToString());

        return Task.FromResult(OutboxItemHandleResult.Ok);
    }
}