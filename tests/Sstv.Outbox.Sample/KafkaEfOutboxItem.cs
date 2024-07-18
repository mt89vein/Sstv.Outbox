using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sstv.Outbox.Sample;

internal sealed class KafkaEfOutboxItemConfiguration : IEntityTypeConfiguration<KafkaEfOutboxItem>
{
    public void Configure(EntityTypeBuilder<KafkaEfOutboxItem> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(x => x.Id);
        builder
            .Property(x => x.Id)
            .ValueGeneratedNever();

        builder
            .Property(x => x.Headers)
            .HasColumnType("json");
    }
}

/// <summary>
/// Outbox item.
/// </summary>
public sealed class KafkaEfOutboxItem : IKafkaOutboxItem, IHasStatus
{
    /// <summary>
    /// Unique identifier.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Message key.
    /// </summary>
    public byte[]? Key { get; set; }

    /// <summary>
    /// Message body.
    /// </summary>
    public byte[]? Value { get; set; }

    /// <summary>
    /// Topic to publish.
    /// </summary>
    public string? Topic { get; set; }

    /// <summary>
    /// Timestamp of event/message.
    /// </summary>
    public DateTimeOffset? Timestamp { get; set; }

    /// <summary>
    /// Headers.
    /// </summary>
    public Dictionary<string, string>? Headers { get; set; }

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
}

/// <summary>
/// Notification message example.
/// </summary>
/// <param name="Id">Unique identifier.</param>
/// <param name="CreatedAt">Timestamp.</param>
/// <param name="Text">Message.</param>
public record NotificationMessage(
    Guid Id,
    DateTimeOffset CreatedAt,
    string Text
);
