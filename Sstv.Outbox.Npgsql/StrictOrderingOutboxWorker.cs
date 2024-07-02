using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Sstv.Outbox.Npgsql;

/// <summary>
/// All workers active, but only one do his job at the same time.
/// </summary>
internal sealed partial class StrictOrderingOutboxWorker : IOutboxWorker
{
    private readonly NpgsqlDataSource _npgsqlDataSource;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StrictOrderingOutboxWorker> _logger;
    private string? _outboxName;

    public StrictOrderingOutboxWorker(
        NpgsqlDataSource npgsqlDataSource,
        IServiceScopeFactory scopeFactory,
        ILogger<StrictOrderingOutboxWorker>? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(npgsqlDataSource);
        ArgumentNullException.ThrowIfNull(scopeFactory);

        _npgsqlDataSource = npgsqlDataSource;
        _scopeFactory = scopeFactory;
        _logger = logger ?? new NullLogger<StrictOrderingOutboxWorker>();
    }

    /// <summary>
    /// Process once.
    /// </summary>
    /// <param name="outboxOptions">Settings.</param>
    /// <param name="ct">Token for cancel operation.</param>
    public async Task ProcessAsync<TOutboxItem>(OutboxOptions outboxOptions, CancellationToken ct = default)
        where TOutboxItem : class, IOutboxItem
    {
        _outboxName ??= typeof(TOutboxItem).Name;

        NpgsqlTransaction? transaction = null;
        try
        {
            await using var connection = await _npgsqlDataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
            transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
            var items = await LockAndReturnItemsBatchAsync<TOutboxItem>(transaction, outboxOptions)
                .ConfigureAwait(false);

            if (items.TryGetNonEnumeratedCount(out var count) && count == 0)
            {
                OutboxItemsEmpty();

                await transaction.CommitAsync(ct).ConfigureAwait(false);

                return;
            }

            if (count != 0)
            {
                OutboxItemFetched(count);
                OutboxMetricCollector.IncFetchedCount(_outboxName, count);
                if (count == outboxOptions.OutboxItemsLimit)
                {
                    OutboxMetricCollector.IncFullBatchFetchedCount(_outboxName);
                }
            }

            var processed = new List<TOutboxItem>(outboxOptions.OutboxItemsLimit);

            foreach (var item in items)
            {
                await using var scope = _scopeFactory.CreateAsyncScope();

                var handler = scope.ServiceProvider.GetRequiredService<IOutboxItemHandler<TOutboxItem>>();
                try
                {
                    var result = await handler.HandleAsync(item, ct).ConfigureAwait(false);

                    if (result.IsSuccess())
                    {
                        processed.Add(item);
                    }
                    else
                    {
                        // получили ошибку, поэтому стопаем процесс, коммитим что уже успели сделать

                        break;
                    }
                }
                catch (Exception e)
                {
                    OutboxItemProcessFailed(e);
                    break;
                }
            }

            if (processed.Count > 0)
            {
                OutboxMetricCollector.IncProcessedCount(_outboxName, processed.Count);
                await DeleteAsync(processed, transaction, outboxOptions, ct).ConfigureAwait(false);
            }

            await transaction.CommitAsync(ct).ConfigureAwait(false);

            OutboxItemsProcessResult(processed.Count);
        }
        catch (Exception e)
        {
            OutboxProcessFailed(e);

            if (transaction is not null)
            {
                await transaction.RollbackAsync(ct).ConfigureAwait(false);
            }
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private static Task<IEnumerable<TOutboxItem>> LockAndReturnItemsBatchAsync<TOutboxItem>(
        NpgsqlTransaction transaction,
        OutboxOptions outboxOptions
    ) where TOutboxItem : class, IOutboxItem
    {
        ArgumentNullException.ThrowIfNull(outboxOptions.Mapping);

        var m = outboxOptions.Mapping;
        // TODO: отфильтровывать по метке - processed = false, если используем партиции

        var sql = $"""
                   SELECT * FROM "{m.TableName}"
                   ORDER BY {m.Id} ASC
                   LIMIT {outboxOptions.OutboxItemsLimit}
                   FOR UPDATE NOWAIT;
                   """;

        return transaction.Connection!.QueryAsync<TOutboxItem>(sql, transaction: transaction);
    }

    private static async Task DeleteAsync<TOutboxItem>(
        List<TOutboxItem> outboxItems,
        NpgsqlTransaction transaction,
        OutboxOptions outboxOptions,
        CancellationToken ct
    ) where TOutboxItem : class, IOutboxItem
    {
        // TODO: Delete or mark as completed with drop partitions (daily/weekly)?
        ArgumentNullException.ThrowIfNull(outboxOptions.Mapping);

        var m = outboxOptions.Mapping;

        const string IDS = "ids";
        var sql = $"""
                   DELETE FROM "{m.TableName}"
                   WHERE {m.Id} in (select * from unnest(@{IDS}));
                   """;

        await using var cmd = transaction.Connection!.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.Add(new NpgsqlParameter<Guid[]>(IDS, outboxItems.Select(o => o.Id).ToArray()));

        await cmd.ExecuteNonQueryAsync(ct);
    }

    [LoggerMessage(
        eventId: 0,
        level: LogLevel.Error,
        message: "OutboxItemHandler failed"
    )]
    private partial void OutboxItemProcessFailed(Exception e);

    [LoggerMessage(
        eventId: 1,
        level: LogLevel.Error,
        message: "Unable to process outbox items"
    )]
    private partial void OutboxProcessFailed(Exception e);

    [LoggerMessage(
        eventId: 2,
        level: LogLevel.Information,
        message: "There is no outbox items"
    )]
    private partial void OutboxItemsEmpty();

    [LoggerMessage(
        eventId: 4,
        level: LogLevel.Debug,
        message: "Outbox items fetched {Count}"
    )]
    private partial void OutboxItemFetched(int count);

    [LoggerMessage(
        eventId: 5,
        level: LogLevel.Debug,
        message: "Outbox items processed {Processed}"
    )]
    private partial void OutboxItemsProcessResult(int processed);
}