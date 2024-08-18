namespace Sstv.Outbox;

/// <summary>
/// Processing outbox items with statuses.
/// </summary>
/// <param name="Enabled">Enabled or not.</param>
public sealed record StatusFeature(bool Enabled);

/// <summary>
/// Extensions for <see cref="OutboxOptions"/>.
/// </summary>
public static partial class OutboxOptionsExtensions
{
    private const string StatusFeature = "StatusFeature";

    /// <summary>
    /// Sets status feature config.
    /// </summary>
    /// <param name="options">Options.</param>
    /// <typeparam name="TOutboxItem">Type of outbox item.</typeparam>
    internal static void SetStatusFeature<TOutboxItem>(this OutboxOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Set(StatusFeature, new StatusFeature(
            Enabled: typeof(TOutboxItem).IsAssignableTo(typeof(IHasStatus))
        ));
    }

    /// <summary>
    /// Returns status feature.
    /// </summary>
    /// <param name="options">Options.</param>
    /// <returns>StatusFeature.</returns>
    public static StatusFeature GetStatusFeature(this OutboxOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Get<StatusFeature>(StatusFeature);
    }
}
