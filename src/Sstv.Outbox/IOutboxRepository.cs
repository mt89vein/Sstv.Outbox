namespace Sstv.Outbox;

/// <summary>
/// Outbox items repository.
/// </summary>
/// <typeparam name="TOutboxItem">Outbox item.</typeparam>
public interface IOutboxRepository<TOutboxItem> : IDisposable, IAsyncDisposable
    where TOutboxItem : class, IOutboxItem
{
    /// <summary>
    /// Lock, fetch and return outbox items.
    /// </summary>
    /// <param name="ct">Token for cancel operation.</param>
    /// <returns>OutboxItems.</returns>
    Task<IEnumerable<TOutboxItem>> LockAndReturnItemsBatchAsync(CancellationToken ct = default);

    /// <summary>
    /// Saves results.
    /// </summary>
    /// <param name="completed">Completed outbox items.</param>
    /// <param name="retried">Retries outbox items.</param>
    /// <param name="ct">Token for cancel operation.</param>
    Task SaveAsync(
        IReadOnlyCollection<TOutboxItem> completed,
        IReadOnlyCollection<TOutboxItem> retried,
        CancellationToken ct = default
    );
}
