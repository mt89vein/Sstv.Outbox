using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Data.Common;

namespace Sstv.Outbox.Npgsql;

/// <summary>
/// Implements API for single active worker.
/// </summary>
public sealed class StrictOrderingOutboxRepository<TOutboxItem> : IOutboxRepository<TOutboxItem>
    where TOutboxItem : class, IOutboxItem
{
    private readonly OutboxOptions _options;
    private NpgsqlConnection? _connection;
    private NpgsqlTransaction? _transaction;

    /// <summary>
    /// Creates new instance of <see cref="StrictOrderingOutboxRepository{TOutboxItem}"/>.
    /// </summary>
    /// <param name="options">Outbox Options.</param>
    public StrictOrderingOutboxRepository(IOptionsMonitor<OutboxOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Get(typeof(TOutboxItem).Name);
    }

    /// <summary>
    /// Starts transaction.
    /// </summary>
    /// <param name="ct">Token for cancel operation.</param>
    /// <returns>Transaction.</returns>
    public async Task<DbTransaction> BeginTransactionAsync(CancellationToken ct = default)
    {
        _connection = await _options.GetNpgsqlDataSource().OpenConnectionAsync(ct);
        return _transaction = await _connection.BeginTransactionAsync(ct);
    }

    /// <summary>
    /// Lock, fetch and return outbox items.
    /// </summary>
    /// <param name="ct">Token for cancel operation.</param>
    /// <returns>OutboxItems.</returns>
    public Task<IEnumerable<TOutboxItem>> LockAndReturnItemsBatchAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (_transaction is null)
        {
            throw new InvalidOperationException("Transaction was null");
        }

        var m = _options.GetDbMapping();
        // TODO: отфильтровывать по метке - processed = false, если используем партиции

        var sql = $"""
                   SELECT * FROM "{m.TableName}"
                   ORDER BY {m.Id} ASC
                   LIMIT {_options.OutboxItemsLimit}
                   FOR UPDATE NOWAIT;
                   """;

        return _transaction.Connection!.QueryAsync<TOutboxItem>(sql, transaction: _transaction);
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

        var m = _options.GetDbMapping();

        // TODO: Delete or mark as completed with drop partitions (daily/weekly)?
        const string IDS = "ids";
        var sql = $"""
                   DELETE FROM "{m.TableName}"
                   WHERE {m.Id} in (select * from unnest(@{IDS}));
                   """;

        await using var cmd = _transaction!.Connection!.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.Add(new NpgsqlParameter<Guid[]>(IDS, completed.Select(o => o.Id).ToArray()));

        await cmd.ExecuteNonQueryAsync(ct);

        await _transaction.CommitAsync(ct);
    }

    /// <summary>
    /// Cleans resources.
    /// </summary>
    public void Dispose()
    {
        _connection?.Dispose();
        _transaction?.Dispose();
    }

    /// <summary>
    /// Cleans resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }

        if (_transaction != null)
        {
            await _transaction.DisposeAsync();
        }
    }
}