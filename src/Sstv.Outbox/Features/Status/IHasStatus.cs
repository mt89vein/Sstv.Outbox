namespace Sstv.Outbox;

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
