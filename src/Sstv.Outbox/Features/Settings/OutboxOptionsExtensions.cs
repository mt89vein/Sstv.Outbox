namespace Sstv.Outbox;

/// <summary>
/// Extensions for <see cref="OutboxOptions"/>.
/// </summary>
internal static partial class OutboxOptionsExtensions
{
    private const string OutboxNameKey = "OutboxName";

    public static void SetOutboxName(this OutboxOptions options, string outboxName)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(outboxName);

        options.Set(OutboxNameKey, outboxName);
    }

    public static string GetOutboxName(this OutboxOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Get<string>(OutboxNameKey);
    }
}
