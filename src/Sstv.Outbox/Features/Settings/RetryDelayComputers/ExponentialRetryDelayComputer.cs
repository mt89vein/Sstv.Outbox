using System.Diagnostics.CodeAnalysis;

namespace Sstv.Outbox;

/// <summary>
/// Exponential retry delay computer.
/// </summary>
internal sealed class ExponentialRetryDelayComputer : IRetryDelayComputer
{
    /// <summary>
    /// Returns delay between retries.
    /// </summary>
    /// <param name="retrySettings">Settings.</param>
    /// <param name="retryNumber">Retry sequence number.</param>
    /// <returns>Computed delay.</returns>
    [SuppressMessage("Security", "CA5394:Do not use non-cryptographic randomizers.")]
    public TimeSpan Compute(RetrySettings retrySettings, int retryNumber)
    {
        var newDelayTime = TimeSpan.FromSeconds(Math.Pow(2, retryNumber));

        if (newDelayTime > retrySettings.RetryMaxDelay)
        {
            newDelayTime = retrySettings.RetryMaxDelay;
        }

        return newDelayTime + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 300));
    }
}
