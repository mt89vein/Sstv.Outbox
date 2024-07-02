using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Sstv.Outbox.Npgsql;

/// <summary>
/// All workers of this type grabs different N items and process them concurrently.
/// </summary>
internal sealed partial class BatchCompetingOutboxWorker : IOutboxWorker
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<BatchCompetingOutboxWorker> _logger;
    private string? _outboxName;

    public BatchCompetingOutboxWorker(
        TimeProvider timeProvider,
        IServiceScopeFactory scopeFactory,
        ILogger<BatchCompetingOutboxWorker>? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);

        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _logger = logger ?? new NullLogger<BatchCompetingOutboxWorker>();
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
            await using var connection = await outboxOptions
                .GetNpgsqlDataSource()
                .OpenConnectionAsync(ct)
                .ConfigureAwait(false);

            transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);

            var items = await LockAndReturnItemsBatchAsync<TOutboxItem>(transaction, outboxOptions)
                .ConfigureAwait(false);

            if (items.TryGetNonEnumeratedCount(out var count) && count == 0)
            {
                OutboxItemsEmpty();

                await transaction.CommitAsync(ct).ConfigureAwait(false);

                return;
            }

            await using var scope = _scopeFactory.CreateAsyncScope();

            var handler = scope.ServiceProvider.GetRequiredService<IOutboxItemBatchHandler<TOutboxItem>>();

            if (items is not List<TOutboxItem> listItems)
            {
                listItems = [.. items];
            }

            OutboxItemFetched(listItems.Count);
            OutboxMetricCollector.IncFetchedCount(_outboxName, listItems.Count);
            if (listItems.Count == outboxOptions.OutboxItemsLimit)
            {
                OutboxMetricCollector.IncFullBatchFetchedCount(_outboxName);
            }

            var result = await handler.HandleAsync(listItems.AsReadOnly(), outboxOptions, ct).ConfigureAwait(false);

            var processed = new List<TOutboxItem>(outboxOptions.OutboxItemsLimit);
            var retried = new List<IHasStatus>();

            foreach (var item in listItems)
            {
                if (!result.TryGetValue(item.Id, out var outboxItemHandleResult))
                {
                    // warn: not resturned result?

                    continue;
                }

                if (outboxItemHandleResult.IsSuccess())
                {
                    processed.Add(item);
                }
                else
                {
                    if (item is IHasStatus hasStatus)
                    {
                        retried.Add(Retry(hasStatus, outboxOptions.RetrySettings));
                    }
                    else
                    {
                        break;
                    }
                }
            }

            if (processed.Count > 0)
            {
                OutboxMetricCollector.IncProcessedCount(_outboxName, processed.Count);
                await DeleteAsync(processed, transaction, outboxOptions, ct).ConfigureAwait(false);
            }

            if (retried.Count > 0)
            {
                OutboxMetricCollector.IncRetriedCount(_outboxName, retried.Count);
                await UpdateAsync(retried, transaction, outboxOptions, ct).ConfigureAwait(false);
            }

            await transaction.CommitAsync(ct).ConfigureAwait(false);

            OutboxItemsProcessResult(processed.Count, retried.Count);
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

    private IHasStatus Retry(IHasStatus outboxItem, RetrySettings retrySettings)
    {
        if (retrySettings.IsEnabled)
        {
            outboxItem.Status = OutboxItemStatus.Retry;
            outboxItem.RetryCount = outboxItem.RetryCount.GetValueOrDefault(0) + 1;
            outboxItem.RetryAfter = _timeProvider
                .GetUtcNow()
                .Add(retrySettings.RetryDelayComputer.Compute(retrySettings, outboxItem.RetryCount.Value));

            if (retrySettings.LogOnRetry)
            {
                OutboxItemScheduledForRetrying(outboxItem.Id);
            }
        }

        return outboxItem;
    }

    private static Task<IEnumerable<TOutboxItem>> LockAndReturnItemsBatchAsync<TOutboxItem>(
        NpgsqlTransaction transaction,
        OutboxOptions outboxOptions
    ) where TOutboxItem : class, IOutboxItem
    {
        var m = outboxOptions.GetDbMapping();

        var sql = $"""
                   SELECT * FROM "{m.TableName}"
                   WHERE {m.RetryAfter} is null or {m.RetryAfter} <= @now
                   ORDER BY {m.Id} ASC
                   LIMIT {outboxOptions.OutboxItemsLimit}
                   FOR UPDATE SKIP LOCKED;
                   """;

        return transaction.Connection!.QueryAsync<TOutboxItem>(sql, transaction: transaction,
            param: new { now = DateTimeOffset.UtcNow }
        );
    }

    private static async Task DeleteAsync<TOutboxItem>(
        List<TOutboxItem> outboxItems,
        NpgsqlTransaction transaction,
        OutboxOptions outboxOptions,
        CancellationToken ct
    ) where TOutboxItem : class, IOutboxItem
    {
        // TODO: Delete or mark as completed with drop partitions (daily/weekly)?
        var m = outboxOptions.GetDbMapping();

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

    private static async Task UpdateAsync(
        List<IHasStatus> outboxItems,
        NpgsqlTransaction transaction,
        OutboxOptions outboxOptions,
        CancellationToken ct
    )
    {
        var m = outboxOptions.GetDbMapping();
        var sql = $"""
                   UPDATE "{m.TableName}"
                   SET "{m.Status}" = data."{m.Status}",
                       "{m.RetryCount}" = data."{m.RetryCount}",
                       "{m.RetryAfter}"  = data."{m.RetryAfter}"
                   FROM (SELECT * FROM unnest(@{m.Id}, @{m.Status}, @{m.RetryCount}, @{m.RetryAfter}))
                                    AS data("{m.Id}", "{m.Status}", "{m.RetryCount}", "{m.RetryAfter}")
                   WHERE "{m.TableName}"."{m.Id}" = data."{m.Id}";
                   """;

        await using var cmd = transaction.Connection!.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.Add(new NpgsqlParameter<Guid[]>(m.Id, outboxItems.Select(e => e.Id).ToArray()));
        cmd.Parameters.Add(new NpgsqlParameter<int[]>(m.Status, outboxItems.Select(e => (int)e.Status).ToArray()));
        cmd.Parameters.Add(new NpgsqlParameter<int?[]>(m.RetryCount, outboxItems.Select(e => e.RetryCount).ToArray()));
        cmd.Parameters.Add(new NpgsqlParameter<DateTimeOffset?[]>(m.RetryAfter, outboxItems.Select(e => e.RetryAfter).ToArray()));

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
        eventId: 3,
        level: LogLevel.Information,
        message: "Outbox item {Id} scheduled for retrying"
    )]
    private partial void OutboxItemScheduledForRetrying(Guid id);

    [LoggerMessage(
        eventId: 4,
        level: LogLevel.Debug,
        message: "Outbox items fetched {Count}"
    )]
    private partial void OutboxItemFetched(int count);

    [LoggerMessage(
        eventId: 5,
        level: LogLevel.Debug,
        message: "Outbox items processed {Processed}, retried {Retried}"
    )]
    private partial void OutboxItemsProcessResult(int processed, int retried);
}