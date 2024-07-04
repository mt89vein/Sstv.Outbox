using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Sstv.Outbox;

/// <summary>
/// All workers of this type grabs different N items and process them concurrently.
/// </summary>
internal sealed partial class BatchCompetingOutboxWorker : IOutboxWorker
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<BatchCompetingOutboxWorker> _logger;

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

            var result = await handler.HandleAsync(listItems.AsReadOnly(), outboxOptions, ct).ConfigureAwait(false);

            var processed = new List<TOutboxItem>(outboxOptions.OutboxItemsLimit);
            var retry = new List<TOutboxItem>();

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