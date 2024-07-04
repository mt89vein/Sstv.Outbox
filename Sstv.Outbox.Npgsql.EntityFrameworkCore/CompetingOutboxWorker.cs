using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics.CodeAnalysis;

namespace Sstv.Outbox.Npgsql.EntityFrameworkCore;

/// <summary>
/// All workers of this type grabs different N items and process them concurrently.
/// </summary>
internal sealed partial class CompetingOutboxWorker<TDbContext> : IOutboxWorker
    where TDbContext : DbContext
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CompetingOutboxWorker<TDbContext>> _logger;

    public CompetingOutboxWorker(
        TimeProvider timeProvider,
        IServiceScopeFactory scopeFactory,
        ILogger<CompetingOutboxWorker<TDbContext>>? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);

        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _logger = logger ?? new NullLogger<CompetingOutboxWorker<TDbContext>>();
    }

    /// <summary>
    /// Process once.
    /// </summary>
    /// <param name="outboxOptions">Settings.</param>
    /// <param name="ct">Token for cancel operation.</param>
    public async Task ProcessAsync<TOutboxItem>(OutboxOptions outboxOptions, CancellationToken ct = default)
        where TOutboxItem : class, IOutboxItem
    {
        var outboxName = outboxOptions.GetOutboxName();

        IDbContextTransaction? transaction = null;
        try
        {
            await using var _ = _scopeFactory.CreateAsyncScope();
            await using var ctx = _.ServiceProvider.GetRequiredService<TDbContext>();
            var dbSet = ctx.Set<TOutboxItem>();

            transaction = await ctx.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

            var items = await LockAndReturnItemsBatchAsync(dbSet, outboxOptions, ct)
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
                OutboxMetricCollector.IncFetchedCount(outboxName, count);
                if (count == outboxOptions.OutboxItemsLimit)
                {
                    OutboxMetricCollector.IncFullBatchFetchedCount(outboxName);
                }
            }

            var processed = new List<TOutboxItem>(outboxOptions.OutboxItemsLimit);
            var retry = new List<TOutboxItem>();

            foreach (var item in items)
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetRequiredService<IOutboxItemHandler<TOutboxItem>>();

                try
                {
                    var result = await handler.HandleAsync(item, outboxOptions, ct).ConfigureAwait(false);

                    if (result.IsSuccess())
                    {
                        processed.Add(item);
                    }
                    else
                    {
                        if (item is IHasStatus hasStatus)
                        {
                            retry.Add((TOutboxItem)Retry(hasStatus, outboxOptions.RetrySettings));
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                catch (Exception e)
                {
                    OutboxItemProcessFailed(e);

                    if (item is IHasStatus hasStatus)
                    {
                        retry.Add((TOutboxItem)Retry(hasStatus, outboxOptions.RetrySettings));
                    }
                    else
                    {
                        break;
                    }
                }
            }

            if (processed.Count > 0)
            {
                OutboxMetricCollector.IncProcessedCount(outboxName, processed.Count);
                dbSet.RemoveRange(processed);
            }

            if (retry.Count > 0)
            {
                OutboxMetricCollector.IncRetriedCount(outboxName, retry.Count);
                dbSet.UpdateRange(retry);
            }

            await ctx.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            OutboxItemsProcessResult(processed.Count, retry.Count);
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

    [SuppressMessage("Security", "EF1002:Risk of vulnerability to SQL injection.")]
    private static async Task<IEnumerable<TOutboxItem>> LockAndReturnItemsBatchAsync<TOutboxItem>(
        DbSet<TOutboxItem> dbSet,
        OutboxOptions outboxOptions,
        CancellationToken ct
    ) where TOutboxItem : class, IOutboxItem
    {
        var m = outboxOptions.GetDbMapping();

        return await dbSet.FromSqlRaw(
                $"""
                 SELECT * FROM {m.TableName}
                 WHERE {m.RetryAfter} is null or {m.RetryAfter} <= '{DateTimeOffset.UtcNow:O}'::timestamptz
                 ORDER BY {m.Id} ASC
                 LIMIT {outboxOptions.OutboxItemsLimit}
                 FOR UPDATE SKIP LOCKED;
                 """)
            .AsTracking()
            .TagWith("CompetingOutboxWorker:LockAndReturnItemsBatchAsync")
            .ToListAsync(ct);
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