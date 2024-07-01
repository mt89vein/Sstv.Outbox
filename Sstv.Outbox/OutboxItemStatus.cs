namespace Sstv.Outbox;

/// <summary>
/// Outbox item status.
/// </summary>
public enum OutboxItemStatus
{
    /// <summary>
    /// Ready to process.
    /// </summary>
    Ready = 0,

    /// <summary>
    /// Processing failed.
    /// </summary>
    Failed = 1,

    /// <summary>
    /// Retrying.
    /// </summary>
    Retry = 2,
}

/// <summary>
/// Outbox processing status.
/// </summary>
public interface IHasStatus : IOutboxItem
{
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