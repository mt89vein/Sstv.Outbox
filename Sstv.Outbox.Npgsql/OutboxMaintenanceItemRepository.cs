using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Sstv.Outbox.Npgsql;

/// <summary>
/// Outbox items repository.
/// </summary>
/// <typeparam name="TOutboxItem">OutboxItem.</typeparam>
internal sealed class OutboxMaintenanceItemRepository<TOutboxItem> : IOutboxMaintenanceRepository<TOutboxItem>
    where TOutboxItem : class, IOutboxItem
{
    /// <summary>
    /// Outbox settings.
    /// </summary>
    private readonly OutboxOptions _outboxOptions;

    /// <summary>
    /// Creates new instance of <see cref="OutboxMaintenanceItemRepository{TOutboxItem}"/>.
    /// </summary>
    /// <param name="monitor">Outbox settings.</param>
    public OutboxMaintenanceItemRepository(IOptionsMonitor<OutboxOptions> monitor)
    {
        _outboxOptions = monitor.Get(typeof(TOutboxItem).Name);
    }

    /// <summary>
    /// Returns chunk of <typeparamref name="TOutboxItem"/>.
    /// </summary>
    /// <param name="skip">How many records skip.</param>
    /// <param name="take">How many records return.</param>
    /// <param name="ct">Token for cancel operation.</param>
    /// <returns>Page of OutboxItems.</returns>
    public async Task<IEnumerable<TOutboxItem>> GetChunkAsync(
        int skip = 0,
        int take = 100,
        CancellationToken ct = default
    )
    {
        await using var connection = await _outboxOptions.GetNpgsqlDataSource().OpenConnectionAsync(ct);

        var m = _outboxOptions.GetDbMapping();
        var sql = $"""
                   SELECT * FROM "{m.TableName}"
                   ORDER BY {m.Id} ASC
                   LIMIT @take
                   OFFSET @skip;
                   """;

        return await connection.QueryAsync<TOutboxItem>(sql, param: new { skip, take });
    }

    /// <summary>
    /// Clear outbox table.
    /// </summary>
    /// <param name="ct">Token for cancel operation.</param>
    public async Task ClearAsync(CancellationToken ct = default)
    {
        await using var connection = await _outboxOptions.GetNpgsqlDataSource().OpenConnectionAsync(ct);
        await using var cmd = connection.CreateCommand();

        var m = _outboxOptions.GetDbMapping();
        var sql = $"TRUNCATE TABLE {m.TableName};";

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
        await using var connection = await _outboxOptions.GetNpgsqlDataSource().OpenConnectionAsync(ct);
        await using var cmd = connection.CreateCommand();

        var m = _outboxOptions.GetDbMapping();
        const string IDS = "ids";
        var sql = $"""
                   DELETE FROM "{m.TableName}"
                   WHERE {m.Id} in (select * from unnest(@{IDS}));
                   """;

        cmd.CommandText = sql;
        cmd.Parameters.Add(new NpgsqlParameter<Guid[]>(IDS, ids));

        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Restart outbox items by ids.
    /// </summary>
    /// <param name="ids">Ids.</param>
    /// <param name="ct">Token for cancel operation.</param>
    public async Task RestartAsync(Guid[] ids, CancellationToken ct = default)
    {
        await using var connection = await _outboxOptions.GetNpgsqlDataSource().OpenConnectionAsync(ct);
        await using var cmd = connection.CreateCommand();

        var m = _outboxOptions.GetDbMapping();
        const string IDS = "ids";
        const string STATUS = "status";
        var sql = $"""
                   UPDATE "{m.TableName}"
                   SET "{m.Status}" = @{STATUS},
                       "{m.RetryCount}" = null,
                       "{m.RetryAfter}" = null
                   WHERE "{m.TableName}"."{m.Id}" = ANY(@{IDS});
                   """;

        cmd.CommandText = sql;
        cmd.Parameters.Add(new NpgsqlParameter<Guid[]>(IDS, ids));
        cmd.Parameters.Add(new NpgsqlParameter<int>(STATUS, (int)OutboxItemStatus.Ready));

        await cmd.ExecuteNonQueryAsync(ct);
    }
}