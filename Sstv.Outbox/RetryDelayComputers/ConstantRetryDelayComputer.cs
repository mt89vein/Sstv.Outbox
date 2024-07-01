namespace Sstv.Outbox;

/// <summary>
/// Constant retry delay computer that returns constant time from config.
/// </summary>
internal sealed class ConstantRetryDelayComputer : IRetryDelayComputer
{
    /// <summary>
    /// Returns delay between retries.
    /// </summary>
    /// <param name="retrySettings">Settings.</param>
    /// <param name="retryNumber">Retry sequence number.</param>
    /// <returns>Computed delay.</returns>
    public TimeSpan Compute(RetrySettings retrySettings, int retryNumber)
    {
        return retrySettings.RetryDelay;
    }
}