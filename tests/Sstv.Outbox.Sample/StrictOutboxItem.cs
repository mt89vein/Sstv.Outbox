using System.ComponentModel.DataAnnotations.Schema;

namespace Sstv.Outbox.Sample;

/// <summary>
/// Outbox item.
/// </summary>
public sealed class StrictOutboxItem : IOutboxItem
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
}

/// <summary>
/// Handler for StrictOutboxItem.
/// </summary>
public sealed class StrictOutboxItemHandler : IOutboxItemHandler<StrictOutboxItem>
{
    /// <summary>
    /// Processes outbox item.
    /// </summary>
    /// <param name="item">Outbox item.</param>
    /// <param name="options">Outbox options.</param>
    /// <param name="ct">Token for cancelling operation.</param>
    public Task<OutboxItemHandleResult> HandleAsync(
        StrictOutboxItem item,
        OutboxOptions options,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(item);

        Console.WriteLine("StrictOutboxItem Handled {0}", item.Id.ToString());

        return Task.FromResult(OutboxItemHandleResult.Ok);
    }
}
