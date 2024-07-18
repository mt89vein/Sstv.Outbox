namespace Sstv.Outbox;

/// <summary>
/// Extension methods for <see cref="OutboxItemHandleResult"/>.
/// </summary>
internal static class OutboxItemHandleResultExtensions
{
    /// <summary>
    /// Returns true, if success result.
    /// </summary>
    /// <param name="result">Result of outbox item processing.</param>
    public static bool IsSuccess(this OutboxItemHandleResult result)
    {
        return result is OutboxItemHandleResult.Ok or OutboxItemHandleResult.Skip;
    }
}
