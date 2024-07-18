namespace Sstv.Outbox.EntityFrameworkCore.Npgsql;

/// <summary>
/// Extensions for <see cref="OutboxOptions"/>.
/// </summary>
internal static class OutboxOptionsExtensions
{
    private const string DbMappingKey = nameof(DbMapping);

    public static void SetDbMapping(this OutboxOptions options, DbMapping mapping)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(mapping);

        options.Set(DbMappingKey, mapping);
    }

    public static DbMapping GetDbMapping(this OutboxOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Get<DbMapping>(DbMappingKey);
    }
}
