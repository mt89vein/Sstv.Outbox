namespace Sstv.Outbox;

/// <summary>
/// An outbox item.
/// </summary>
public interface IOutboxItem
{
    /// <summary>
    /// Unique identifier.
    /// </summary>
    public Guid Id { get; }
}