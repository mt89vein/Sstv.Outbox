using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Sstv.Outbox;

/// <summary>
/// Outbox items processing metric collector.
/// </summary>
public static class OutboxMetricCollector
{
    /// <summary>
    /// Meter name.
    /// </summary>
    public const string MeterName = "Sstv.Outbox";

    /// <summary>
    /// Meter instance.
    /// </summary>
    private static readonly Meter Meter = new(MeterName);

    /// <summary>
    /// Measures duration of worker process one batch.
    /// </summary>
    private static readonly Histogram<long> BatchProcessTime = Meter.CreateHistogram<long>(
        name: "outbox.worker.process.duration",
        description: "Measures duration of worker process one batch."
    );

    /// <summary>
    /// Measures duration of worker sleep between batches.
    /// </summary>
    private static readonly Histogram<long> WorkerSleepTime = Meter.CreateHistogram<long>(
        name: "outbox.worker.sleep.duration",
        description: "Measures duration of worker sleep between batches."
    );

    /// <summary>
    /// Measures duration of processing by outbox item handler.
    /// </summary>
    private static readonly Histogram<long> OutboxItemHandlerProcessTime = Meter.CreateHistogram<long>(
        name: "outbox.worker.handler.duration",
        description: "Measures duration of processing by outbox item handler."
    );

    /// <summary>
    /// Counts how many outbox items fetched.
    /// </summary>
    private static readonly Counter<long> FetchedCount = Meter.CreateCounter<long>(
        name: "outbox.items.fetched",
        description: "Counts how many outbox items fetched."
    );

    /// <summary>
    /// Counts how many outbox items processed.
    /// </summary>
    private static readonly Counter<long> ProcessedCount = Meter.CreateCounter<long>(
        name: "outbox.items.processed",
        description: "Counts how many outbox items processed."
    );

    /// <summary>
    /// Counts how many outbox items retried.
    /// </summary>
    private static readonly Counter<long> RetriedCount = Meter.CreateCounter<long>(
        name: "outbox.items.retried",
        description: "Counts how many outbox items retried."
    );

    /// <summary>
    /// Counts how many times fetched full batches.
    /// </summary>
    private static readonly Counter<long> FullBatchesCount = Meter.CreateCounter<long>(
        name: "outbox.items.full-batches",
        description: "Counts how many times fetched full batches."
    );

    /// <summary>
    /// Measures duration of worker process one batch.
    /// </summary>
    /// <param name="elapsedMilliseconds">duration in ms.</param>
    /// <param name="outboxName">Name of outbox type.</param>
    public static void RecordBatchProcessTime(long elapsedMilliseconds, string outboxName)
    {
        BatchProcessTime.Record(elapsedMilliseconds, CreateTags(outboxName));
    }

    /// <summary>
    /// Measures duration of worker sleep between batches.
    /// </summary>
    /// <param name="elapsedMilliseconds">duration in ms.</param>
    /// <param name="outboxName">Name of outbox type.</param>
    public static void RecordWorkerSleepDuration(long elapsedMilliseconds, string outboxName)
    {
        WorkerSleepTime.Record(elapsedMilliseconds, CreateTags(outboxName));
    }

    /// <summary>
    /// Measures duration of processing by outbox item handler.
    /// </summary>
    /// <param name="elapsedMilliseconds">duration in ms.</param>
    /// <param name="outboxName">Name of outbox type.</param>
    /// <param name="batched">Batched handler or not.</param>
    public static void RecordOutboxItemHandlerProcessTime(
        long elapsedMilliseconds,
        string outboxName,
        bool batched
    )
    {
        OutboxItemHandlerProcessTime.Record(elapsedMilliseconds, CreateTags(outboxName, batched));
    }

    /// <summary>
    /// Counts how many outbox items retried.
    /// </summary>
    /// <param name="outboxName">Name of outbox type.</param>
    /// <param name="retried">How many outbox items retried.</param>
    public static void IncRetriedCount(string outboxName, int retried)
    {
        RetriedCount.Add(retried, CreateTags(outboxName));
    }

    /// <summary>
    /// Counts how many outbox items processed.
    /// </summary>
    /// <param name="outboxName">Name of outbox type.</param>
    /// <param name="processed">How many outbox items processed.</param>
    public static void IncProcessedCount(string outboxName, int processed)
    {
        ProcessedCount.Add(processed, CreateTags(outboxName));
    }

    /// <summary>
    /// Counts how many outbox items fetched.
    /// </summary>
    /// <param name="outboxName">Name of outbox type.</param>
    /// <param name="fetched">How many outbox items fetched.</param>
    public static void IncFetchedCount(string outboxName, int fetched)
    {
        FetchedCount.Add(fetched, CreateTags(outboxName));
    }

    /// <summary>
    /// Counts how many times fetched full batches.
    /// </summary>
    /// <param name="outboxName">Name of outbox type.</param>
    public static void IncFullBatchFetchedCount(string outboxName)
    {
        FullBatchesCount.Add(1, CreateTags(outboxName));
    }

    private static TagList CreateTags(string outboxName, bool? batched = null)
    {
        if (batched.HasValue)
        {
            return new TagList
            {
                { "outbox_name", outboxName },
                { "batched", batched.ToString() },
            };
        }

        return new TagList
        {
            { "outbox_name", outboxName },
        };
    }
}
