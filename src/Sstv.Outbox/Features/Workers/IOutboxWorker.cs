namespace Sstv.Outbox;

/// <summary>
/// Worker that processes outbox items.
/// </summary>
public interface IOutboxWorker
{
    /// <summary>
    /// Process once.
    /// </summary>
    /// <param name="outboxOptions">Settings.</param>
    /// <param name="ct">Token for cancel operation.</param>
    public Task ProcessAsync<TOutboxItem>(OutboxOptions outboxOptions, CancellationToken ct = default)
        where TOutboxItem : class, IOutboxItem;
}
