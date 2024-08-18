using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Sstv.Outbox.EntityFrameworkCore.Npgsql;

/// <summary>
/// Implements API for single active worker.
/// </summary>
public sealed class StrictOrderingOutboxRepository<TDbContext, TOutboxItem> : IOutboxRepository<TOutboxItem>
    where TDbContext : DbContext
    where TOutboxItem : class, IOutboxItem
{
    private readonly TDbContext _dbContext;
    private readonly OutboxOptions _options;
    private IDbContextTransaction? _transaction;

    /// <summary>
    /// Creates new instance of <see cref="StrictOrderingOutboxRepository{TDbContext,TOutboxItem}"/>.
    /// </summary>
    /// <param name="dbContext">DbContext.</param>
    /// <param name="options">Outbox options.</param>
    public StrictOrderingOutboxRepository(
        TDbContext dbContext,
        IOptionsMonitor<OutboxOptions> options
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(dbContext);

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
        var dbSet = _dbContext.Set<TOutboxItem>();

        _transaction = await _dbContext.Database.BeginTransactionAsync(ct);

        var filter = _options.PartitionSettings.Enabled
            ? $"WHERE {m.Status} <> {(int)OutboxItemStatus.Completed}"
            : string.Empty;

        var order = _options.GetPriorityFeature()
            .Enabled
            ? $"ORDER BY {m.Priority} DESC, {m.Id} ASC"
            : $"ORDER BY {m.Id} ASC";

        var sql = $"""
                   SELECT * FROM {m.QualifiedTableName}
                   {filter}
                   {order}
                   LIMIT {_options.OutboxItemsLimit}
                   FOR UPDATE NOWAIT;
                   """;

        try
        {
            return await dbSet.FromSqlRaw(sql)
                .AsTracking()
                .TagWith("StrictOrderingOutboxRepository:LockAndReturnItemsBatchAsync")
                .ToListAsync(ct);
        }
        catch (InvalidOperationException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.LockNotAvailable })
        {
            return Array.Empty<TOutboxItem>();
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.LockNotAvailable)
        {
            return Array.Empty<TOutboxItem>();
        }
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
        CancellationToken ct
    )
    {
        ArgumentNullException.ThrowIfNull(completed);
        ArgumentNullException.ThrowIfNull(retried);

        if (_transaction is null)
        {
            throw new InvalidOperationException("Transaction was null");
        }

        if (retried.Count != 0)
        {
            throw new NotSupportedException("Retry not supported for strict ordering worker");
        }

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
