using System.ComponentModel.DataAnnotations;

namespace Sstv.Outbox;

/// <summary>
/// Outbox settings.
/// </summary>
public sealed class OutboxOptions
{
    /// <summary>
    /// Table name.
    /// </summary>
    public string? TableName { get; set; }

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
    /// Metadata.
    /// </summary>
    internal Dictionary<string, object> Metadata { get; } = new();
}