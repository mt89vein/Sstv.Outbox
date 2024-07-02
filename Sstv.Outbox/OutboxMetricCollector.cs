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
    public const string METER_NAME = "Sstv.Outbox";

    /// <summary>
    /// Meter instance.
    /// </summary>
    private static readonly Meter _meter = new(METER_NAME);

    /// <summary>
    /// Measured duration of worker process one batch.
    /// </summary>
    private static readonly Histogram<long> _batchProcessTime = _meter.CreateHistogram<long>(
        name: "outbox.worker.duration",
        description: "Measured duration of worker process one batch."
    );

    /// <summary>
    /// Counts how many outbox items fetched.
    /// </summary>
    private static readonly Counter<long> _fetchedCount = _meter.CreateCounter<long>(
        name: "outbox.items.fetched",
        description: "Counts how many outbox items fetched."
    );

    /// <summary>
    /// Counts how many outbox items processed.
    /// </summary>
    private static readonly Counter<long> _processedCount = _meter.CreateCounter<long>(
        name: "outbox.items.processed",
        description: "Counts how many outbox items processed."
    );

    /// <summary>
    /// Counts how many outbox items retried.
    /// </summary>
    private static readonly Counter<long> _retriedCount = _meter.CreateCounter<long>(
        name: "outbox.items.retried",
        description: "Counts how many outbox items retried."
    );

    /// <summary>
    /// Counts how many times fetched full batches.
    /// </summary>
    private static readonly Counter<long> _fullBatchesCount = _meter.CreateCounter<long>(
        name: "outbox.items.full-batches",
        description: "Counts how many times fetched full batches."
    );

    /// <summary>
    /// Measured duration of worker process one batch.
    /// </summary>
    /// <param name="elapsedMilliseconds">duration in ms.</param>
    /// <param name="outboxName">Name of outbox type.</param>
    public static void RecordBatchProcessTime(long elapsedMilliseconds, string outboxName)
    {
        _batchProcessTime.Record(elapsedMilliseconds, CreateTags(outboxName));
    }

    /// <summary>
    /// Counts how many outbox items retried.
    /// </summary>
    /// <param name="outboxName">Name of outbox type.</param>
    /// <param name="retried">How many outbox items retried.</param>
    public static void IncRetriedCount(string outboxName, int retried)
    {
        _retriedCount.Add(retried, CreateTags(outboxName));
    }

    /// <summary>
    /// Counts how many outbox items processed.
    /// </summary>
    /// <param name="outboxName">Name of outbox type.</param>
    /// <param name="processed">How many outbox items processed.</param>
    public static void IncProcessedCount(string outboxName, int processed)
    {
        _processedCount.Add(processed, CreateTags(outboxName));
    }

    /// <summary>
    /// Counts how many outbox items fetched.
    /// </summary>
    /// <param name="outboxName">Name of outbox type.</param>
    /// <param name="fetched">How many outbox items fetched.</param>
    public static void IncFetchedCount(string outboxName, int fetched)
    {
        _fetchedCount.Add(fetched, CreateTags(outboxName));
    }

    /// <summary>
    /// Counts how many times fetched full batches.
    /// </summary>
    /// <param name="outboxName">Name of outbox type.</param>
    public static void IncFullBatchFetchedCount(string outboxName)
    {
        _fullBatchesCount.Add(1, CreateTags(outboxName));
    }

    private static TagList CreateTags(string outboxName)
    {
        return new TagList
        {
            { "outbox_name", outboxName },
        };
    }
}