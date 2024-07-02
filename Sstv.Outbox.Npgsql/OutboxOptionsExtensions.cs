using Npgsql;

namespace Sstv.Outbox.Npgsql;

/// <summary>
/// Extensions for <see cref="OutboxOptions"/>.
/// </summary>
internal static class OutboxOptionsExtensions
{
    private const string NPGSQL_DATA_SOURCE_KEY = nameof(NpgsqlDataSource);
    private const string DB_MAPPING_KEY = nameof(DbMapping);

    public static void SetNpgsqlDataSource(this OutboxOptions options, NpgsqlDataSource npgsqlDataSource)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(npgsqlDataSource);

        options.Set(NPGSQL_DATA_SOURCE_KEY, npgsqlDataSource);
    }

    public static NpgsqlDataSource GetNpgsqlDataSource(this OutboxOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Get<NpgsqlDataSource>(NPGSQL_DATA_SOURCE_KEY);
    }

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

    private static void Set<T>(this OutboxOptions options, string key, T value)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        options.Metadata[key] = value;
    }

    private static T Get<T>(this OutboxOptions options, string key)
        where T : class
    {
        if (!options.Metadata.TryGetValue(key, out var metadata))
        {
            throw new InvalidOperationException($"{key} not set");
        }

        return (T)metadata;
    }
}