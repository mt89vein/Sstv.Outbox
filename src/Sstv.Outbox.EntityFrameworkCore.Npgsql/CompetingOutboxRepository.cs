using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace Sstv.Outbox.EntityFrameworkCore.Npgsql;

/// <summary>
/// Implements API for concurrent work of multiple workers.
/// </summary>
public sealed class CompetingOutboxRepository<TDbContext, TOutboxItem> : IOutboxRepository<TOutboxItem>
    where TDbContext : DbContext
    where TOutboxItem : class, IOutboxItem
{
    private readonly TDbContext _dbContext;
    private readonly OutboxOptions _options;
    private IDbContextTransaction? _transaction;

    /// <summary>
    /// Creates new instance of <see cref="CompetingOutboxRepository{TDbContext,TOutboxItem}"/>.
    /// </summary>
    /// <param name="dbContext">DbContext.</param>
    /// <param name="options">Outbox options.</param>
    public CompetingOutboxRepository(
        TDbContext dbContext,
        IOptionsMonitor<OutboxOptions> options
    )
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(options);

        _dbContext = dbContext;
        _options = options.Get(typeof(TOutboxItem).Name);
    }

    /// <summary>
    /// Lock, fetch and return outbox items.
    /// </summary>
    /// <param name="ct">Token for cancel operation.</param>
    /// <returns>OutboxItems.</returns>
    [SuppressMessage("Security", "EF1002:Risk of vulnerability to SQL injection.",
        Justification = "There is no user input. FromSqlInterpolated incorrectly sets table name")]
    public async Task<IEnumerable<TOutboxItem>> LockAndReturnItemsBatchAsync(CancellationToken ct = default)
    {
        var m = _options.GetDbMapping();

        _transaction = await _dbContext.Database.BeginTransactionAsync(ct);

        var filter = _options.PartitionSettings.Enabled
            ? $" and {m.Status} <> {(int)OutboxItemStatus.Completed}"
            : string.Empty;

        var order = _options.GetPriorityFeature()
            .Enabled
            ? $"ORDER BY {m.Priority} DESC, {m.Id} ASC, {m.RetryAfter} ASC"
            : $"ORDER BY {m.Id} ASC, {m.RetryAfter} ASC";

        var sql = $"""
                     SELECT * FROM {m.QualifiedTableName}
                     WHERE ({m.RetryAfter} is null or {m.RetryAfter} <= '{DateTimeOffset.UtcNow:O}'::timestamptz){filter}
                     {order}
                     LIMIT {_options.OutboxItemsLimit}
                     FOR UPDATE SKIP LOCKED;
                   """;

        return await _dbContext
            .Set<TOutboxItem>()
            .FromSqlRaw(sql)
            .AsTracking()
            .TagWith("CompetingOutboxWorker:LockAndReturnItemsBatchAsync")
            .ToListAsync(ct);
    }

    /// <summary>
    /// Saves results.
    /// </summary>
    /// <param name="completed">Completed outbox items.</param>
    /// <param name="retried">Retries outbox items.</param>
    /// <param name="ct">Token for cancel operation.</param>
    public async Task SaveAsync(
        IReadOnlyCollection<TOutboxItem> completed,
        IReadOnlyCollection<TOutboxItem> retried,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(completed);
        ArgumentNullException.ThrowIfNull(retried);

        if (_transaction is null)
        {
            throw new InvalidOperationException("Transaction was null");
        }

        // hint: retried collection tracked by EF, so they automatically updated on save changes
        if (!_options.PartitionSettings.Enabled)
        {
            _dbContext.Set<TOutboxItem>().RemoveRange(completed);
        }

        await _dbContext.SaveChangesAsync(ct);
        await _transaction.CommitAsync(ct);
    }

    /// <summary>
    /// Cleans resources.
    /// </summary>
    public void Dispose()
    {
        _transaction?.Dispose();
    }

    /// <summary>
    /// Cleans resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_transaction != null)
        {
            await _transaction.DisposeAsync();
        }
    }
}
