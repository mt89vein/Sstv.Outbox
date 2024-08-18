using System.ComponentModel.DataAnnotations.Schema;

namespace Sstv.Outbox.Sample;

/// <summary>
/// Outbox item.
/// </summary>
public sealed class PartitionedOutboxItem : IOutboxItem, IHasStatus
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
    /// Headers.
    /// </summary>
    [Column("headers", TypeName = "bytea")]
    public byte[]? Headers { get; set; }

    /// <summary>
    /// Data.
    /// </summary>
    [Column("data", TypeName = "bytea")]
    public byte[]? Data { get; set; }

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
}

/// <summary>
/// Handler for PartitionedStrictOutboxItem.
/// </summary>
public sealed class PartitionedOutboxItemHandler : IOutboxItemHandler<PartitionedOutboxItem>
{
    /// <summary>
    /// Processes outbox item.
    /// </summary>
    /// <param name="item">Outbox item.</param>
    /// <param name="options">Outbox options.</param>
    /// <param name="ct">Token for cancelling operation.</param>
    public Task<OutboxItemHandleResult> HandleAsync(
        PartitionedOutboxItem item,
        OutboxOptions options,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(item);

        Console.WriteLine("PartitionedStrictOutboxItem Handled {0}", item.Id.ToString());

        return Task.FromResult(OutboxItemHandleResult.Ok);
    }
}
