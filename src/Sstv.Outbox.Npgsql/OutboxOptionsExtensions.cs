using Npgsql;

namespace Sstv.Outbox.Npgsql;

/// <summary>
/// Extensions for <see cref="OutboxOptions"/>.
/// </summary>
internal static class OutboxOptionsExtensions
{
    private const string NpgsqlDataSourceKey = nameof(NpgsqlDataSource);
    private const string DbMappingKey = nameof(DbMapping);

    public static void SetNpgsqlDataSource(this OutboxOptions options, NpgsqlDataSource npgsqlDataSource)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(npgsqlDataSource);

        options.Set(NpgsqlDataSourceKey, npgsqlDataSource);
    }

    public static NpgsqlDataSource GetNpgsqlDataSource(this OutboxOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Get<NpgsqlDataSource>(NpgsqlDataSourceKey);
    }

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
