namespace Sstv.Outbox;

/// <summary>
/// Processing priority of outbox items.
/// </summary>
/// <param name="Enabled">Enabled or not.</param>
public sealed record PriorityFeature(bool Enabled);

/// <summary>
/// Extensions for <see cref="OutboxOptions"/>.
/// </summary>
public static partial class OutboxOptionsExtensions
{
    private const string PriorityFeature = "PriorityFeature";

    /// <summary>
    /// Sets priority feature.
    /// </summary>
    /// <param name="options">Options.</param>
    /// <typeparam name="TOutboxItem">Type of outbox item.</typeparam>
    internal static void SetPriorityFeature<TOutboxItem>(this OutboxOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Set(PriorityFeature, new PriorityFeature(
            Enabled: typeof(TOutboxItem).IsAssignableTo(typeof(IHasPriority)))
        );
    }

    /// <summary>
    /// Returns priority feature.
    /// </summary>
    /// <param name="options">Options.</param>
    /// <returns>PriorityFeature</returns>
    public static PriorityFeature GetPriorityFeature(this OutboxOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Get<PriorityFeature>(PriorityFeature);
    }
}

