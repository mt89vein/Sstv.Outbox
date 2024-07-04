using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql.NameTranslation;
using System.Globalization;

namespace Sstv.Outbox.Npgsql.EntityFrameworkCore;

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
    /// <param name="dbContext">DbContext.</param>
    /// <returns>Mapping to db.</returns>
    public static DbMapping GetFor<TOutboxItem>(DbContext dbContext)
        where TOutboxItem : class, IOutboxItem
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var dbSet = dbContext.Set<TOutboxItem>();

        var tableName = dbSet.EntityType.GetAnnotation(RelationalAnnotationNames.TableName).Value?.ToString()
                        ?? dbSet.EntityType.GetTableName() ??
                        throw new InvalidOperationException($"Can't get table name for entity of type {typeof(TOutboxItem)} in {dbContext.GetType()}");

        return new DbMapping
        {
            TableName = tableName,
            ColumnNames =
            {
                [nameof(IOutboxItem.Id)] = GetColumnName(dbSet, nameof(IOutboxItem.Id)),
                [nameof(IHasStatus.Status)] = GetColumnName(dbSet, nameof(IHasStatus.Status)),
                [nameof(IHasStatus.RetryAfter)] = GetColumnName(dbSet, nameof(IHasStatus.RetryAfter)),
                [nameof(IHasStatus.RetryCount)] = GetColumnName(dbSet, nameof(IHasStatus.RetryCount)),
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