using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Sstv.Outbox;

/// <summary>
/// All workers of this type grabs different N items and process them concurrently.
/// </summary>
internal sealed partial class CompetingOutboxWorker : IOutboxWorker
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CompetingOutboxWorker> _logger;

    public CompetingOutboxWorker(
        TimeProvider timeProvider,
        IServiceScopeFactory scopeFactory,
        ILogger<CompetingOutboxWorker>? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);

        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _logger = logger ?? new NullLogger<CompetingOutboxWorker>();
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
            var sw = new Stopwatch();

            foreach (var item in items)
            {
                try
                {
                    await using var handlerScope = _scopeFactory.CreateAsyncScope();
                    var handler = handlerScope.ServiceProvider.GetRequiredService<IOutboxItemHandler<TOutboxItem>>();

                    sw.Restart();
                    var result = await handler.HandleAsync(item, outboxOptions, ct).ConfigureAwait(false);
                    sw.Stop();
                    OutboxMetricCollector.RecordOutboxItemHandlerProcessTime(sw.ElapsedMilliseconds, outboxName, batched: false);

                    if (result.IsSuccess())
                    {
                        if (item is IHasStatus hasStatus)
                        {
                            hasStatus.Status = OutboxItemStatus.Completed;
                        }

                        processed.Add(item);
                    }
                    else if (item is IHasStatus hasStatus)
                    {
                        Retry(hasStatus, outboxOptions.RetrySettings);
                        retry.Add(item);
                    }
                    else
                    {
                        break;
                    }
                }
                catch (Exception e)
                {
                    OutboxItemProcessFailed(e);

                    if (item is IHasStatus hasStatus)
                    {
                        Retry(hasStatus, outboxOptions.RetrySettings);
                        retry.Add(item);
                    }
                    else
                    {
                        break;
                    }
                }
            }

            if (processed.Count > 0 || retry.Count > 0)
            {
                OutboxMetricCollector.IncProcessedCount(outboxName, processed.Count);
                OutboxMetricCollector.IncRetriedCount(outboxName, retry.Count);
                await repository.SaveAsync(processed, retry, ct).ConfigureAwait(false);
            }

            OutboxItemsProcessResult(processed.Count, retry.Count);
        }
        catch (Exception e)
        {
            OutboxProcessFailed(e);
        }
    }

    private void Retry(IHasStatus outboxItem, RetrySettings retrySettings)
    {
        if (!retrySettings.IsEnabled)
        {
            return;
        }

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
