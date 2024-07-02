namespace Sstv.Outbox;

/// <summary>
/// Outbox item retry settings.
/// </summary>
public sealed class RetrySettings
{
    /// <summary>
    /// Is retries enabled?
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Calculates delay time between retries.
    /// </summary>
    public IRetryDelayComputer RetryDelayComputer { get; set; } = new ExponentialRetryDelayComputer();

    /// <summary>
    /// Log when sent to retry.
    /// </summary>
    public bool LogOnRetry { get; set; } = true;

    /// <summary>
    /// Defines how many time need to delay per retry.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Maximum delay between retries.
    /// </summary>
    public TimeSpan RetryMaxDelay { get; set; } = TimeSpan.FromSeconds(10);
}