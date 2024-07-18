using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Sstv.Outbox;

/// <summary>
/// All workers active, but only one do his job at the same time.
/// </summary>
internal sealed partial class StrictOrderingOutboxWorker : IOutboxWorker
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StrictOrderingOutboxWorker> _logger;

    public StrictOrderingOutboxWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<StrictOrderingOutboxWorker>? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);

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
                await repository.SaveAsync(processed, Array.Empty<TOutboxItem>(), ct).ConfigureAwait(false);
            }

            OutboxItemsProcessResult(processed.Count);
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
