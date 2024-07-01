using System.ComponentModel.DataAnnotations;

namespace Sstv.Outbox;

/// <summary>
/// Outbox settings.
/// </summary>
public sealed class OutboxOptions
{
    /// <summary>
    /// Worker types.
    /// </summary>
    public string? WorkerType { get; set; }

    /// <summary>
    /// Outbox batch size. By default 100.
    /// </summary>
    public int OutboxItemsLimit { get; set; } = 100;

    /// <summary>
    /// Delay between batch processings. By default 5 seconds.
    /// </summary>
    [Range(typeof(TimeSpan), minimum: "00:00:00", maximum: "01:00:00")]
    public TimeSpan OutboxDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Retrying outbox item settings.
    /// </summary>
    public RetrySettings RetrySettings { get; set; } = new();

    /// <summary>
    /// DbName mapping.
    /// </summary>
    public DbMapping Mapping { get; set; } = null!;
}

/// <summary>
/// DbNamings.
/// </summary>
public sealed class DbMapping
{
    /// <summary>
    /// The name of outbox table.
    /// </summary>
    public string TableName { get; set; } = null!;

    /// <summary>
    /// Mapping of columns.
    /// </summary>
    public Dictionary<string, string> ColumnNames { get; } = new();

    /// <summary>
    /// Id column.
    /// </summary>
    internal string Id => ColumnNames[nameof(IOutboxItem.Id)];

    /// <summary>
    /// Status column.
    /// </summary>
    internal string Status => ColumnNames[nameof(IHasStatus.Status)];

    /// <summary>
    /// Retry after column.
    /// </summary>
    internal string RetryAfter => ColumnNames[nameof(IHasStatus.RetryAfter)];

    /// <summary>
    /// Retry count column.
    /// </summary>
    internal string RetryCount => ColumnNames[nameof(IHasStatus.RetryCount)];
}