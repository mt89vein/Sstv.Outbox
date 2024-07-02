using Npgsql.NameTranslation;
using System.Globalization;

namespace Sstv.Outbox.Npgsql;

/// <summary>
/// DbNamings.
/// </summary>
internal sealed class DbMapping
{
    /// <summary>
    /// The name of outbox table.
    /// </summary>
    public string TableName { get; set; } = null!;

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
    /// Default db mapping.
    /// </summary>
    /// <param name="tableName">OutboxItem table name</param>
    /// <returns>Mapping to db.</returns>
    public static DbMapping GetDefault(string tableName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        var culture = CultureInfo.InvariantCulture;
        return new DbMapping
        {
            TableName = NpgsqlSnakeCaseNameTranslator.ConvertToSnakeCase(tableName, culture),
            ColumnNames =
            {
                [nameof(IOutboxItem.Id)] = NpgsqlSnakeCaseNameTranslator.ConvertToSnakeCase(nameof(IOutboxItem.Id), culture),
                [nameof(IHasStatus.Status)] = NpgsqlSnakeCaseNameTranslator.ConvertToSnakeCase(nameof(IHasStatus.Status), culture),
                [nameof(IHasStatus.RetryAfter)] = NpgsqlSnakeCaseNameTranslator.ConvertToSnakeCase(nameof(IHasStatus.RetryAfter), culture),
                [nameof(IHasStatus.RetryCount)] =  NpgsqlSnakeCaseNameTranslator.ConvertToSnakeCase(nameof(IHasStatus.RetryCount), culture),
            }
        };
    }
}