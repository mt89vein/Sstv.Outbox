namespace Sstv.Outbox;

/// <summary>
/// Result of outbox item processing.
/// </summary>
public enum OutboxItemHandleResult
{
    /// <summary>
    /// Not set.
    /// </summary>
    Undefined = 0,

    /// <summary>
    /// outbox item processed.
    /// </summary>
    Ok = 1,

    /// <summary>
    /// outbox item skipped.
    /// </summary>
    Skip = 2,

    /// <summary>
    /// Outbox item needs to be retried.
    /// </summary>
    Retry = 3
}
