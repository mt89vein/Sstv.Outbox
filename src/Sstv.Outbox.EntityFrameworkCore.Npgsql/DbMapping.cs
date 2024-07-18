using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql.NameTranslation;

namespace Sstv.Outbox.EntityFrameworkCore.Npgsql;

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
    /// <param name="dbContext">DbContext.</param>
    /// <returns>Mapping to db.</returns>
    public static DbMapping GetFor<TOutboxItem>(DbContext dbContext)
        where TOutboxItem : class, IOutboxItem
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var dbSet = dbContext.Set<TOutboxItem>();

        var tableName = dbSet.EntityType.GetTableName() ??
                        throw new InvalidOperationException($"Can't get table name for entity of type {typeof(TOutboxItem)} in {dbContext.GetType()}");

        var schemaName = dbSet.EntityType.GetDefaultSchema() ?? "public";

        return new DbMapping
        {
            SchemaName = schemaName,
            TableName = tableName,
            QualifiedTableName = schemaName + "." + $"\"{tableName}\"",
            ColumnNames =
            {
                [nameof(IOutboxItem.Id)] = GetColumnName(dbSet, nameof(IOutboxItem.Id)),
                [nameof(IHasStatus.Status)] = GetColumnName(dbSet, nameof(IHasStatus.Status)),
                [nameof(IHasStatus.RetryAfter)] = GetColumnName(dbSet, nameof(IHasStatus.RetryAfter)),
                [nameof(IHasStatus.RetryCount)] = GetColumnName(dbSet, nameof(IHasStatus.RetryCount)),
                [nameof(IHasPriority.Priority)] = GetColumnName(dbSet, nameof(IHasPriority.Priority)),
            }
        };
    }

    private static string GetColumnName<TOutboxItem>(DbSet<TOutboxItem> dbSet, string name)
        where TOutboxItem : class, IOutboxItem
    {
        try
        {
            // annotations set by naming conventions, like UseSnakeCaseNamingConvention()
            return dbSet.EntityType
                       .GetProperty(name)
                       .GetAnnotation(RelationalAnnotationNames.ColumnName).Value?.ToString() ??
                   NpgsqlSnakeCaseNameTranslator.ConvertToSnakeCase(name, CultureInfo.InvariantCulture);
        }
        catch (InvalidOperationException)
        {
            return NpgsqlSnakeCaseNameTranslator.ConvertToSnakeCase(name, CultureInfo.InvariantCulture);
        }
    }
}
