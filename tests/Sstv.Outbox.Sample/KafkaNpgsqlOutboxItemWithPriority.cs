using System.ComponentModel.DataAnnotations.Schema;

namespace Sstv.Outbox.Sample;

/// <summary>
/// Outbox item.
/// </summary>
public sealed class KafkaNpgsqlOutboxItemWithPriority : IKafkaOutboxItem, IHasStatus, IHasPriority
{
    /// <summary>
    /// Unique identifier.
    /// </summary>
    [Column("id", TypeName = "uuid")]
    public Guid Id { get; init; }

    /// <summary>
    /// Message key.
    /// </summary>
    [Column("key", TypeName = "bytea")]
    public byte[]? Key { get; set; }

    /// <summary>
    /// Message body.
    /// </summary>
    [Column("value", TypeName = "bytea")]
    public byte[]? Value { get; set; }

    /// <summary>
    /// Topic to publish.
    /// </summary>
    [Column("topic", TypeName = "text")]
    public string? Topic { get; set; }

    /// <summary>
    /// Headers.
    /// </summary>
    [Column("headers", TypeName = "jsonb")]
    public Dictionary<string, string>? Headers { get; set; }

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
    /// Priority of processing.
    /// Higher number, higher priority.
    /// </summary>
    [Column("priority", TypeName = "int4")]
    public int Priority { get; set; }

    /// <summary>
    /// Timestamp of event/message.
    /// </summary>
    [Column("timestamp", TypeName = "timestamptz")]
    public DateTimeOffset? Timestamp { get; set; }
}
