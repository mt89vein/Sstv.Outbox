using System.Diagnostics.CodeAnalysis;
using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Sstv.Outbox.Npgsql;

/// <summary>
/// Implements API for concurrent work of multiple workers.
/// </summary>
[SuppressMessage("Security", "CA2100:sql injection check", Justification = "There is no user input in sql")]
public sealed class CompetingOutboxRepository<TOutboxItem> : IOutboxRepository<TOutboxItem>
    where TOutboxItem : class, IOutboxItem
{
    private readonly OutboxOptions _options;
    private NpgsqlConnection? _connection;
    private NpgsqlTransaction? _transaction;

    /// <summary>
    /// Creates new instance of <see cref="CompetingOutboxRepository{TOutboxItem}"/>.
    /// </summary>
    /// <param name="options">Outbox options.</param>
    public CompetingOutboxRepository(IOptionsMonitor<OutboxOptions> options)
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

        string sql;
        if (_options.GetPriorityFeature().Enabled)
        {
            sql = $"""
                   SELECT * FROM {m.QualifiedTableName}
                   WHERE {m.RetryAfter} is null or {m.RetryAfter} <= @now
                   ORDER BY {m.Priority} DESC, {m.Id} ASC, {m.RetryAfter} ASC
                   LIMIT {_options.OutboxItemsLimit}
                   FOR UPDATE SKIP LOCKED;
                   """;
        }
        else
        {
            sql = $"""
                   SELECT * FROM {m.QualifiedTableName}
                   WHERE {m.RetryAfter} is null or {m.RetryAfter} <= @now
                   ORDER BY {m.Id} ASC, {m.RetryAfter} ASC
                   LIMIT {_options.OutboxItemsLimit}
                   FOR UPDATE SKIP LOCKED;
                   """;
        }

        return await _connection.QueryAsync<TOutboxItem>(sql, transaction: _transaction,
            param: new { now = DateTimeOffset.UtcNow }
        );
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

        if (_connection is null)
        {
            throw new InvalidOperationException("Connection was null");
        }

        if (_transaction is null)
        {
            throw new InvalidOperationException("Transaction was null");
        }

        var m = _options.GetDbMapping();

        if (completed.Count > 0)
        {
            // TODO: Delete or mark as completed with drop partitions (daily/weekly)?
            const string ids = "ids";
            var sql = $"""
                       DELETE FROM {m.QualifiedTableName}
                       WHERE {m.Id} in (select * from unnest(@{ids}));
                       """;

            await using var cmd = _connection!.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.Add(new NpgsqlParameter<Guid[]>(ids, completed.Select(o => o.Id).ToArray()));

            await cmd.ExecuteNonQueryAsync(ct);
        }

        if (retried.Count > 0)
        {
            var sql = $"""
                       UPDATE {m.QualifiedTableName}
                       SET "{m.Status}" = data."{m.Status}",
                           "{m.RetryCount}" = data."{m.RetryCount}",
                           "{m.RetryAfter}"  = data."{m.RetryAfter}"
                       FROM (SELECT * FROM unnest(@{m.Id}, @{m.Status}, @{m.RetryCount}, @{m.RetryAfter}))
                                        AS data("{m.Id}", "{m.Status}", "{m.RetryCount}", "{m.RetryAfter}")
                       WHERE {m.QualifiedTableName}."{m.Id}" = data."{m.Id}";
                       """;

            await using var cmd = _connection!.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.Add(new NpgsqlParameter<Guid[]>(m.Id, retried.Select(e => e.Id).ToArray()));
            cmd.Parameters.Add(new NpgsqlParameter<int[]>(m.Status, retried.Select(e => (int)((IHasStatus)e).Status).ToArray()));
            cmd.Parameters.Add(new NpgsqlParameter<int?[]>(m.RetryCount, retried.Select(e => ((IHasStatus)e).RetryCount).ToArray()));
            cmd.Parameters.Add(new NpgsqlParameter<DateTimeOffset?[]>(m.RetryAfter, retried.Select(e => ((IHasStatus)e).RetryAfter).ToArray()));
            await cmd.ExecuteNonQueryAsync(ct);
        }

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
