namespace Sstv.Outbox;

/// <summary>
/// Extensions for <see cref="OutboxOptions"/>.
/// </summary>
internal static partial class OutboxOptionsExtensions
{
    private const string PriorityFeature = "PriorityFeature";

    public static void SetPriorityFeature<TOutboxItem>(this OutboxOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Set(PriorityFeature, new PriorityFeature(
            Enabled: typeof(TOutboxItem).IsAssignableTo(typeof(IHasPriority)))
        );
    }

    public static PriorityFeature GetPriorityFeature(this OutboxOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Get<PriorityFeature>(PriorityFeature);
    }
}
