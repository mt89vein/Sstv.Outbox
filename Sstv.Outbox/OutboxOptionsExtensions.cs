namespace Sstv.Outbox;

/// <summary>
/// Extensions for <see cref="OutboxOptions"/>.
/// </summary>
internal static class OutboxOptionsExtensions
{
    private const string OUTBOX_NAME = "OutboxName";

    public static void SetOutboxName(this OutboxOptions options, string outboxName)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(outboxName);

        options.Set(OUTBOX_NAME, outboxName);
    }

    public static string GetOutboxName(this OutboxOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Get<string>(OUTBOX_NAME);
    }
}