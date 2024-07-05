using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Data;
using System.Diagnostics.CodeAnalysis;

namespace Sstv.Outbox.Npgsql.EntityFrameworkCore;

/// <summary>
/// Outbox items maintenance repository.
/// </summary>
/// <typeparam name="TDbContext">DbContext.</typeparam>
/// <typeparam name="TOutboxItem">OutboxItem.</typeparam>
public sealed class OutboxMaintenanceItemRepository<TDbContext, TOutboxItem> : IOutboxMaintenanceRepository<TOutboxItem>
    where TDbContext : DbContext
    where TOutboxItem : class, IOutboxItem
{
    /// <summary>
    /// DbContext.
    /// </summary>
    private readonly TDbContext _dbContext;

    /// <summary>
    /// Outbox settings.
    /// </summary>
    private readonly OutboxOptions _outboxOptions;

    /// <summary>
    /// Creates new instance of <see cref="OutboxMaintenanceItemRepository{TDbContext,TOutboxItem}"/>.
    /// </summary>
    /// <param name="dbContext">DbContext.</param>
    /// <param name="monitor">Outbox settings.</param>
    public OutboxMaintenanceItemRepository(
        TDbContext dbContext,
        IOptionsMonitor<OutboxOptions> monitor)
    {
        _dbContext = dbContext;
        ArgumentNullException.ThrowIfNull(monitor);
        _outboxOptions = monitor.Get(typeof(TOutboxItem).Name);
    }

    /// <summary>
    /// Returns chunk of <typeparamref name="TOutboxItem"/>.
    /// </summary>
    /// <param name="skip">How many records skip.</param>
    /// <param name="take">How many records return.</param>
    /// <param name="ct">Token for cancel operation.</param>
    /// <returns>Page of OutboxItems.</returns>
    [SuppressMessage("Security", "EF1002:Risk of vulnerability to SQL injection.",
        Justification = "There is no user input. FromSqlInterpolated incorrectly sets table name")]
    public async Task<IEnumerable<TOutboxItem>> GetChunkAsync(
        int skip,
        int take,
        CancellationToken ct = default
    )
    {
        return await _dbContext
            .Set<TOutboxItem>()
            .OrderBy(x => x.Id)
            .Take(take)
            .Skip(skip)
            .ToArrayAsync(ct);
    }

    /// <summary>
    /// Clear outbox table.
    /// </summary>
    /// <param name="ct">Token for cancel operation.</param>
    public async Task ClearAsync(CancellationToken ct = default)
    {
        var connection = (NpgsqlConnection)_dbContext.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        await using var cmd = connection.CreateCommand();

        var m = _outboxOptions.GetDbMapping();
        var sql = $"TRUNCATE TABLE {m.QualifiedTableName};";

        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Delete outbox items by ids.
    /// </summary>
    /// <param name="ids">Ids.</param>
    /// <param name="ct">Token for cancel operation.</param>
    public async Task DeleteAsync(Guid[] ids, CancellationToken ct = default)
    {
        await _dbContext
            .Set<TOutboxItem>()
            .Where(x => ids.Contains(x.Id))
            .ExecuteDeleteAsync(ct);

        await _dbContext.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Restart outbox items by ids.
    /// </summary>
    /// <param name="ids">Ids.</param>
    /// <param name="ct">Token for cancel operation.</param>
    public async Task RestartAsync(Guid[] ids, CancellationToken ct = default)
    {
        await _dbContext
            .Set<TOutboxItem>()
            .Where(x => ids.Contains(x.Id))
            .ExecuteUpdateAsync(x => x
                .SetProperty(i => ((IHasStatus)i).Status, OutboxItemStatus.Ready)
                .SetProperty(i => ((IHasStatus)i).RetryCount, (int?)null)
                .SetProperty(i => ((IHasStatus)i).RetryAfter, (DateTimeOffset?)null), cancellationToken: ct);

        await _dbContext.SaveChangesAsync(ct);
    }
}