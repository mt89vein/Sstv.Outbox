namespace Sstv.Outbox;

/// <summary>
/// Calculates delay time between retries.
/// </summary>
public interface IRetryDelayComputer
{
    /// <summary>
    /// Returns delay between retries.
    /// </summary>
    /// <param name="retrySettings">Settings.</param>
    /// <param name="retryNumber">Retry sequence number.</param>
    /// <returns>Computed delay.</returns>
    TimeSpan Compute(RetrySettings retrySettings, int retryNumber);
}
