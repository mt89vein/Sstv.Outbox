using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Text.Json;
using Dapper;

namespace Sstv.Outbox.Sample;

/// <summary>
/// Outbox item.
/// </summary>
public sealed class KafkaNpgsqlOutboxItem : IKafkaOutboxItem, IHasStatus
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
    /// Timestamp of event/message.
    /// </summary>
    [Column("timestamp", TypeName = "timestamptz")]
    public DateTimeOffset? Timestamp { get; set; }
}

internal sealed class JsonbHeadersHandler : SqlMapper.TypeHandler<Dictionary<string, string>>
{
    public override void SetValue(IDbDataParameter parameter, Dictionary<string, string>? value)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        parameter.Value = JsonSerializer.Serialize(value);
    }

    public override Dictionary<string, string>? Parse(object value)
    {
        return value is string s
            ? JsonSerializer.Deserialize<Dictionary<string, string>>(s)
            : null;
    }
}
