using System.ComponentModel.DataAnnotations;

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

        if (typeof(T) != metadata.GetType())
        {
            throw new InvalidOperationException($"{key} type {metadata.GetType()} not matched with requested {typeof(T)}");
        }

        return (T)metadata;
    }
}