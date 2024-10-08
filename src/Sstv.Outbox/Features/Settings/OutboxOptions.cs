using System.ComponentModel.DataAnnotations;
using Sstv.Outbox.Features.Partitions;

namespace Sstv.Outbox;

/// <summary>
/// Outbox settings.
/// </summary>
public sealed class OutboxOptions
{
    /// <summary>
    /// Metadata.
    /// </summary>
    private readonly Dictionary<string, object> _metadata = new();

    /// <summary>
    /// Is worker enabled.
    /// </summary>
    public bool IsWorkerEnabled { get; set; } = true;

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
    public TimeSpan WorkerDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Retrying outbox item settings.
    /// </summary>
    public RetrySettings RetrySettings { get; set; } = new();

    /// <summary>
    /// Table parttioning settings.
    /// </summary>
    public PartitionSettings PartitionSettings { get; set; } = new();

    /// <summary>
    /// Uuid generator.
    /// </summary>
    public required Func<Guid> NextGuid { get; set; } = UUIDNext.Uuid.NewSequential;

    /// <summary>
    /// Current time provider.
    /// </summary>
    public required Func<DateTimeOffset> CurrentTime { get; set; } = TimeProvider.System.GetUtcNow;

    /// <summary>
    /// Set <paramref name="value"/> using <paramref name="key"/>.
    /// </summary>
    /// <param name="key">Key.</param>
    /// <param name="value">Value.</param>
    /// <typeparam name="T">Type of value.</typeparam>
    public void Set<T>(string key, T value)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        _metadata[key] = value;
    }

    /// <summary>
    /// Returns <typeparamref name="T"/> using <paramref name="key"/>.
    /// </summary>
    /// <param name="key">Key.</param>
    /// <typeparam name="T">Type of value.</typeparam>
    /// <exception cref="InvalidOperationException">When key is not set or type not matched.</exception>
    /// <returns>Value.</returns>
    public T Get<T>(string key)
        where T : class
    {
        if (!_metadata.TryGetValue(key, out var metadata))
        {
            throw new InvalidOperationException($"{key} not set");
        }

        if (!metadata.GetType().IsAssignableTo(typeof(T)))
        {
            throw new InvalidOperationException($"{key} type {metadata.GetType()} not matched with requested {typeof(T)}");
        }

        return (T)metadata;
    }
}

/// <summary>
/// Extensions for <see cref="OutboxOptions"/>.
/// </summary>
public static partial class OutboxOptionsExtensions
{
    private const string OutboxNameKey = "OutboxName";

    /// <summary>
    /// Sets the name of outbox.
    /// </summary>
    /// <param name="options">Options.</param>
    /// <param name="outboxName">The name of outbox.</param>
    internal static void SetOutboxName(this OutboxOptions options, string outboxName)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(outboxName);

        options.Set(OutboxNameKey, outboxName);
    }

    /// <summary>
    /// Returns name of outbox.
    /// </summary>
    /// <param name="options">Options.</param>
    /// <returns>Name.</returns>
    public static string GetOutboxName(this OutboxOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Get<string>(OutboxNameKey);
    }
}
