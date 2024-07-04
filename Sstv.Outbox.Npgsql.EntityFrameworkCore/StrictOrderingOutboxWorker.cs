using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics.CodeAnalysis;

namespace Sstv.Outbox.Npgsql.EntityFrameworkCore;

/// <summary>
/// All workers active, but only one do his job at the same time.
/// </summary>
public partial class StrictOrderingOutboxWorker<TDbContext> : IOutboxWorker
    where TDbContext : DbContext
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StrictOrderingOutboxWorker<TDbContext>> _logger;

    /// <summary>
    /// Creates new instance of <see cref="StrictOrderingOutboxWorker{TDbContext}"/>.
    /// </summary>
    /// <param name="scopeFactory">DI Scope factory.</param>
    /// <param name="logger">Logger.</param>
    public StrictOrderingOutboxWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<StrictOrderingOutboxWorker<TDbContext>>? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);

        _scopeFactory = scopeFactory;
        _logger = logger ?? new NullLogger<StrictOrderingOutboxWorker<TDbContext>>();
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
                OutboxMetricCollector.IncProcessedCount(outboxName, processed.Count);

                // TODO: Delete or mark as completed with drop partitions (daily/weekly)?
                dbSet.RemoveRange(processed);
            }

            await ctx.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

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

    /// <summary>
    /// Locks and return batch of outbox items.
    /// </summary>
    /// <param name="dbSet">DbSet.</param>
    /// <param name="outboxOptions">Options.</param>
    /// <param name="ct">Token for cancel operation.</param>
    /// <typeparam name="TOutboxItem">Type of outbox item.</typeparam>
    /// <returns>Locked outbox items.</returns>
    [SuppressMessage("Security", "EF1002:Risk of vulnerability to SQL injection.")]
    protected virtual async Task<IEnumerable<TOutboxItem>> LockAndReturnItemsBatchAsync<TOutboxItem>(
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
        level: LogLevel.Debug,
        message: "Outbox items fetched {Count}"
    )]
    private partial void OutboxItemFetched(int count);

    [LoggerMessage(
        eventId: 4,
        level: LogLevel.Debug,
        message: "Outbox items processed {Processed}"
    )]
    private partial void OutboxItemsProcessResult(int processed);
}