using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;

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
    /// Lock, fetch and return outbox items.
    /// </summary>
    /// <param name="ct">Token for cancel operation.</param>
    /// <returns>OutboxItems.</returns>
    public async Task<IEnumerable<TOutboxItem>> LockAndReturnItemsBatchAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        _connection = await _options.GetNpgsqlDataSource().OpenConnectionAsync(ct);
        _transaction = await _connection.BeginTransactionAsync(ct);

        var m = _options.GetDbMapping();
        // TODO: отфильтровывать по метке - processed = false, если используем партиции

        var sql = $"""
                   SELECT * FROM {m.QualifiedTableName}
                   ORDER BY {m.Id} ASC
                   LIMIT {_options.OutboxItemsLimit}
                   FOR UPDATE NOWAIT;
                   """;

        return await _connection.QueryAsync<TOutboxItem>(sql, transaction: _transaction);
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
                   DELETE FROM {m.QualifiedTableName}
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