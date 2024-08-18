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

    /// <summary>
    /// Completed.
    /// </summary>
    Completed = 3,
}
