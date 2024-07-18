using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace Sstv.Outbox.Sample;

/// <summary>
/// Outbox item.
/// </summary>
public sealed class MyOutboxItem : IOutboxItem, IHasStatus
{
    /// <summary>
    /// Unique identifier.
    /// </summary>
    [Column("id", TypeName = "uuid")]
    public Guid Id { get; init; }

    /// <summary>
    /// Created at timestamp.
    /// </summary>
    [Column("created_at", TypeName = "timestamptz")]
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Status.
    /// </summary>
    [Column("status", TypeName = "int4")]
    public OutboxItemStatus Status { get; set; }

    /// <summary>
    /// How many times retried already.
    /// </summary>
    [Column("retry_count", TypeName = "int4")]
    public int? RetryCount { get; set; }

    /// <summary>
    /// When to retry.
    /// </summary>
    [Column("retry_after", TypeName = "timestamptz")]
    public DateTimeOffset? RetryAfter { get; set; }

    /// <summary>
    /// Headers.
    /// </summary>
    [Column("headers", TypeName = "bytea")]
    public byte[]? Headers { get; set; }

    /// <summary>
    /// Data.
    /// </summary>
    [Column("data", TypeName = "bytea")]
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
    [SuppressMessage("Security", "CA5394:Do not use non-cryptographic randomizers.")]
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
