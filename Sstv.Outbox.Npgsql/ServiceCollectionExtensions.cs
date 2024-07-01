using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql.NameTranslation;
using System.Globalization;

namespace Sstv.Outbox.Npgsql;

/// <summary>
/// Worker types that can be used.
/// </summary>
public static class NpgsqlWorkerTypes
{
    /// <summary>
    /// All workers active, but only one do his job at the same time.
    /// </summary>
    public const string STRICT_ORDERING = "npgsql_strict_ordering";

    /// <summary>
    /// All workers of this type grabs different N items and process them concurrently.
    /// </summary>
    public const string COMPETING = "npgsql_competing";

    /// <summary>
    /// All workers of this type grabs different N items and process them concurrently.
    /// Batch passed to client handler.
    /// </summary>
    public const string BATCH_COMPETING = "npgsql_batch_competing";
}

/// <summary>
/// Extensions methods for <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Already registered outbox items.
    /// </summary>
    private static readonly HashSet<Type> _registeredTypes = [];

    /// <summary>
    /// Adds <typeparamref name="TOutboxItem"/> handling services.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configure"></param>
    /// <typeparam name="TOutboxItem">Outbox item type.</typeparam>
    /// <typeparam name="TOutboxItemHandler">Outbox item handler type.</typeparam>
    public static void AddOutboxItem<TOutboxItem, TOutboxItemHandler>(
        this IServiceCollection services,
        Action<OutboxOptions>? configure = null
    )
        where TOutboxItem : class, IOutboxItem
        where TOutboxItemHandler : class, IOutboxItemHandler<TOutboxItem>
    {
        var type = typeof(TOutboxItem);
        if (_registeredTypes.Contains(type))
        {
            throw new InvalidOperationException($"The {type} already registered!");
        }

        services
            .AddOptions<OutboxOptions>(type.Name)
            .BindConfiguration($"Outbox:{type.Name}")
            .Configure(o =>
            {
                o.Mapping = new DbMapping
                {
                    TableName = NpgsqlSnakeCaseNameTranslator.ConvertToSnakeCase(type.Name.Pluralize(), CultureInfo.InvariantCulture),
                    ColumnNames =
                    {
                        [nameof(IOutboxItem.Id)] = NpgsqlSnakeCaseNameTranslator.ConvertToSnakeCase(nameof(IOutboxItem.Id), CultureInfo.InvariantCulture),
                        [nameof(IHasStatus.Status)] = NpgsqlSnakeCaseNameTranslator.ConvertToSnakeCase(nameof(IHasStatus.Status), CultureInfo.InvariantCulture),
                        [nameof(IHasStatus.RetryAfter)] = NpgsqlSnakeCaseNameTranslator.ConvertToSnakeCase(nameof(IHasStatus.RetryAfter), CultureInfo.InvariantCulture),
                        [nameof(IHasStatus.RetryCount)] = NpgsqlSnakeCaseNameTranslator.ConvertToSnakeCase(nameof(IHasStatus.RetryCount), CultureInfo.InvariantCulture),
                    }
                };
                o.WorkerType ??= NpgsqlWorkerTypes.COMPETING;

                configure?.Invoke(o);
            })
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddTransient<IOutboxItemHandler<TOutboxItem>, TOutboxItemHandler>();
        services.TryAddKeyedTransient<IOutboxWorker, StrictOrderingOutboxWorker>(NpgsqlWorkerTypes.STRICT_ORDERING);
        services.TryAddKeyedTransient<IOutboxWorker, CompetingOutboxWorker>(NpgsqlWorkerTypes.COMPETING);
        services.TryAddSingleton(TimeProvider.System);
        services.AddHostedService<OutboxBackgroundService<TOutboxItem>>();

        _registeredTypes.Add(type);
    }

    /// <summary>
    /// Adds <typeparamref name="TOutboxItem"/> handling services.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configure"></param>
    /// <typeparam name="TOutboxItem">Outbox item type.</typeparam>
    /// <typeparam name="TOutboxItemHandler">Outbox item handler type.</typeparam>
    public static void AddOutboxItemBatch<TOutboxItem, TOutboxItemHandler>(
        this IServiceCollection services,
        Action<OutboxOptions>? configure = null
    )
        where TOutboxItem : class, IOutboxItem
        where TOutboxItemHandler : class, IOutboxItemBatchHandler<TOutboxItem>
    {
        var type = typeof(TOutboxItem);
        if (_registeredTypes.Contains(type))
        {
            throw new InvalidOperationException($"The {type} already registered!");
        }

        services
            .AddOptions<OutboxOptions>(type.Name)
            .BindConfiguration($"Outbox:{type.Name}")
            .Configure(o =>
            {
                o.Mapping = new DbMapping
                {
                    TableName = NpgsqlSnakeCaseNameTranslator.ConvertToSnakeCase(type.Name.Pluralize(), CultureInfo.InvariantCulture),
                    ColumnNames =
                    {
                        [nameof(IOutboxItem.Id)] = NpgsqlSnakeCaseNameTranslator.ConvertToSnakeCase(nameof(IOutboxItem.Id), CultureInfo.InvariantCulture),
                        [nameof(IHasStatus.Status)] = NpgsqlSnakeCaseNameTranslator.ConvertToSnakeCase(nameof(IHasStatus.Status), CultureInfo.InvariantCulture),
                        [nameof(IHasStatus.RetryAfter)] = NpgsqlSnakeCaseNameTranslator.ConvertToSnakeCase(nameof(IHasStatus.RetryAfter), CultureInfo.InvariantCulture),
                        [nameof(IHasStatus.RetryCount)] = NpgsqlSnakeCaseNameTranslator.ConvertToSnakeCase(nameof(IHasStatus.RetryCount), CultureInfo.InvariantCulture),
                    }
                };
                o.WorkerType ??= NpgsqlWorkerTypes.BATCH_COMPETING;

                configure?.Invoke(o);
            })
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // TODO: NOT register client types?
        services.TryAddTransient<IOutboxItemBatchHandler<TOutboxItem>, TOutboxItemHandler>();
        services.TryAddKeyedTransient<IOutboxWorker, BatchCompetingOutboxWorker>(NpgsqlWorkerTypes.BATCH_COMPETING);
        services.TryAddSingleton(TimeProvider.System);
        services.AddHostedService<OutboxBackgroundService<TOutboxItem>>();

        _registeredTypes.Add(type);
    }
}