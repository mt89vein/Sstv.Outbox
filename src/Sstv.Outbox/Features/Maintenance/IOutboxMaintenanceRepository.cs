namespace Sstv.Outbox;

/// <summary>
/// Outbox items repository.
/// </summary>
/// <typeparam name="TOutboxItem">OutboxItem.</typeparam>
public interface IOutboxMaintenanceRepository<TOutboxItem>
    where TOutboxItem : class, IOutboxItem
{
    /// <summary>
    /// Returns page of <typeparamref name="TOutboxItem"/>.
    /// </summary>
    /// <param name="skip">How many records skip.</param>
    /// <param name="take">How many records return.</param>
    /// <param name="ct">Token for cancel operation.</param>
    /// <returns>Page of OutboxItems.</returns>
    Task<IEnumerable<TOutboxItem>> GetChunkAsync(int skip, int take, CancellationToken ct = default);

    /// <summary>
    /// Clear outbox table.
    /// </summary>
    /// <param name="ct">Token for cancel operation.</param>
    Task ClearAsync(CancellationToken ct = default);

    /// <summary>
    /// Delete outbox items by ids.
    /// </summary>
    /// <param name="ids">Ids.</param>
    /// <param name="ct">Token for cancel operation.</param>
    Task DeleteAsync(Guid[] ids, CancellationToken ct = default);

    /// <summary>
    /// Restart outbox items by ids.
    /// </summary>
    /// <param name="ids">Ids.</param>
    /// <param name="ct">Token for cancel operation.</param>
    Task RestartAsync(Guid[] ids, CancellationToken ct = default);
}
