namespace Sstv.Outbox;

/// <summary>
/// Linear retry delay computer.
/// </summary>
internal sealed class LinearRetryDelayComputer : IRetryDelayComputer
{
    /// <summary>
    /// Returns delay between retries.
    /// </summary>
    /// <param name="retrySettings">Settings.</param>
    /// <param name="retryNumber">Retry sequence number.</param>
    /// <returns>Computed delay.</returns>
    public TimeSpan Compute(RetrySettings retrySettings, int retryNumber)
    {
        TimeSpan newDelayTime;
        try
        {
            checked
            {
                newDelayTime = retryNumber * retrySettings.RetryDelay;
            }
        }
        catch (OverflowException)
        {
            newDelayTime = retrySettings.RetryMaxDelay;
        }

        if (newDelayTime > retrySettings.RetryMaxDelay)
        {
            newDelayTime = retrySettings.RetryMaxDelay;
        }

        return newDelayTime;
    }
}
