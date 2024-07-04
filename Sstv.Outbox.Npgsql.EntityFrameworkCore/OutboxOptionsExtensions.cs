namespace Sstv.Outbox.Npgsql.EntityFrameworkCore;

/// <summary>
/// Extensions for <see cref="OutboxOptions"/>.
/// </summary>
internal static class OutboxOptionsExtensions
{
    private const string DB_MAPPING_KEY = nameof(DbMapping);

    public static void SetDbMapping(this OutboxOptions options, DbMapping mapping)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(mapping);

        options.Set(DB_MAPPING_KEY, mapping);
    }

    public static DbMapping GetDbMapping(this OutboxOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Get<DbMapping>(DB_MAPPING_KEY);
    }
}