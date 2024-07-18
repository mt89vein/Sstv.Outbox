namespace Sstv.Outbox;

/// <summary>
/// An outbox item with priority.
/// </summary>
public interface IHasPriority : IOutboxItem
{
    /// <summary>
    /// Priority of processing.
    /// Higher number, higher priority.
    /// </summary>
    public int Priority { get; set; }
}
