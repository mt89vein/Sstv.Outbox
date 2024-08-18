using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Sstv.Outbox;

/// <summary>
/// All workers active, but only one do his job at the same time.
/// </summary>
internal sealed partial class BatchStrictOrderingOutboxWorker : IOutboxWorker
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<BatchStrictOrderingOutboxWorker> _logger;

    public BatchStrictOrderingOutboxWorker(
        TimeProvider timeProvider,
        IServiceScopeFactory scopeFactory,
        ILogger<BatchStrictOrderingOutboxWorker>? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);

        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _logger = logger ?? new NullLogger<BatchStrictOrderingOutboxWorker>();
    }

    /// <summary>
    /// Process once.
    /// </summary>
    /// <param name="outboxOptions">Settings.</param>
    /// <param name="ct">Token for cancel operation.</param>
    public async Task ProcessAsync<TOutboxItem>(OutboxOptions outboxOptions, CancellationToken ct = default)
        where TOutboxItem : class, IOutboxItem
    {
        try
        {
            var outboxName = outboxOptions.GetOutboxName();
            await using var scope = _scopeFactory.CreateAsyncScope();
            await using var repository = scope.ServiceProvider.GetRequiredKeyedService<IOutboxRepository<TOutboxItem>>(outboxOptions.WorkerType);
            var items = await repository.LockAndReturnItemsBatchAsync(ct).ConfigureAwait(false);

            if (items.TryGetNonEnumeratedCount(out var count) && count == 0)
            {
                OutboxItemsEmpty();

                return;
            }

            var handler = scope.ServiceProvider.GetRequiredService<IOutboxItemBatchHandler<TOutboxItem>>();

            if (items is not List<TOutboxItem> listItems)
            {
                listItems = [.. items];
            }

            OutboxItemFetched(listItems.Count);
            OutboxMetricCollector.IncFetchedCount(outboxName, listItems.Count);
            if (listItems.Count == outboxOptions.OutboxItemsLimit)
            {
                OutboxMetricCollector.IncFullBatchFetchedCount(outboxName);
            }

            var sw = Stopwatch.StartNew();
            var result = await handler.HandleAsync(listItems.AsReadOnly(), outboxOptions, ct).ConfigureAwait(false);
            sw.Stop();
            OutboxMetricCollector.RecordOutboxItemHandlerProcessTime(sw.ElapsedMilliseconds, outboxName, batched: true);

            if (result.AllProcessed)
            {
                if (outboxOptions.PartitionSettings.Enabled)
                {
                    listItems.ForEach(x => ((IHasStatus)x).Status = OutboxItemStatus.Completed);
                }

                OutboxMetricCollector.IncProcessedCount(outboxName, listItems.Count);
                await repository.SaveAsync(listItems, retried: [], ct).ConfigureAwait(false);

                OutboxItemsProcessResult(listItems.Count);
            }
            else
            {
                var processed = new List<TOutboxItem>(outboxOptions.OutboxItemsLimit);

                foreach (var item in listItems)
                {
                    if (!result.ProcessedInfo.TryGetValue(item.Id, out var outboxItemHandleResult))
                    {
                        // warn: not returned result?

                        continue;
                    }

                    if (outboxItemHandleResult.IsSuccess())
                    {
                        if (item is IHasStatus hasStatus)
                        {
                            hasStatus.Status = OutboxItemStatus.Completed;
                        }

                        processed.Add(item);
                    }
                    else
                    {
                        // if we get an error, we stop processing and commit what we have already done

                        break;
                    }
                }

                if (processed.Count > 0)
                {
                    OutboxMetricCollector.IncProcessedCount(outboxName, processed.Count);
                    await repository.SaveAsync(processed, Array.Empty<TOutboxItem>(), ct).ConfigureAwait(false);
                }

                OutboxItemsProcessResult(processed.Count);
            }
        }
        catch (Exception e)
        {
            OutboxProcessFailed(e);
        }
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
        message: "Outbox items processed {Processed}"
    )]
    private partial void OutboxItemsProcessResult(int processed);
}
