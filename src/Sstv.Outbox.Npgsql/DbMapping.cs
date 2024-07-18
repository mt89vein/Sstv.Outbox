using System.Globalization;
using Npgsql.NameTranslation;

namespace Sstv.Outbox.Npgsql;

/// <summary>
/// DbNamings.
/// </summary>
internal sealed class DbMapping
{
    /// <summary>
    /// Schema name where outbox table located.
    /// </summary>
    public string SchemaName { get; init; } = null!;

    /// <summary>
    /// The name of outbox table.
    /// </summary>
    public string TableName { get; init; } = null!;

    /// <summary>
    /// Mapping of columns.
    /// </summary>
    public Dictionary<string, string> ColumnNames { get; } = new();

    /// <summary>
    /// Id column.
    /// </summary>
    internal string Id => ColumnNames[nameof(IOutboxItem.Id)];

    /// <summary>
    /// Status column.
    /// </summary>
    internal string Status => ColumnNames[nameof(IHasStatus.Status)];

    /// <summary>
    /// Retry after column.
    /// </summary>
    internal string RetryAfter => ColumnNames[nameof(IHasStatus.RetryAfter)];

    /// <summary>
    /// Retry count column.
    /// </summary>
    internal string RetryCount => ColumnNames[nameof(IHasStatus.RetryCount)];

    /// <summary>
    /// Priority column.
    /// </summary>
    internal string Priority => ColumnNames[nameof(IHasPriority.Priority)];

    /// <summary>
    /// Table schema name + table name.
    /// </summary>
    internal string QualifiedTableName { get; init; } = null!;

    /// <summary>
    /// Default db mapping.
    /// </summary>
    /// <param name="schemaName">Database schema name where outbox table located..</param>
    /// <param name="tableName">OutboxItem table name</param>
    /// <returns>Mapping to db.</returns>
    public static DbMapping GetDefault(string schemaName, string tableName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaName);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        var culture = CultureInfo.InvariantCulture;
        tableName = NpgsqlSnakeCaseNameTranslator.ConvertToSnakeCase(tableName, culture);

        return new DbMapping
        {
            SchemaName = schemaName,
            TableName = tableName,
            QualifiedTableName = schemaName + "." + $"\"{tableName}\"",
            ColumnNames =
            {
                [nameof(IOutboxItem.Id)] = NpgsqlSnakeCaseNameTranslator.ConvertToSnakeCase(nameof(IOutboxItem.Id), culture),
                [nameof(IHasStatus.Status)] = NpgsqlSnakeCaseNameTranslator.ConvertToSnakeCase(nameof(IHasStatus.Status), culture),
                [nameof(IHasStatus.RetryAfter)] = NpgsqlSnakeCaseNameTranslator.ConvertToSnakeCase(nameof(IHasStatus.RetryAfter), culture),
                [nameof(IHasStatus.RetryCount)] =  NpgsqlSnakeCaseNameTranslator.ConvertToSnakeCase(nameof(IHasStatus.RetryCount), culture),
                [nameof(IHasPriority.Priority)] =  NpgsqlSnakeCaseNameTranslator.ConvertToSnakeCase(nameof(IHasPriority.Priority), culture),
            }
        };
    }
}
