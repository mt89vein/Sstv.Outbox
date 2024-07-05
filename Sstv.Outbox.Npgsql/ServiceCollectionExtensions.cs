using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace Sstv.Outbox.Npgsql;

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
    /// <param name="services">Service registrator.</param>
    /// <param name="schemaName">Database schema name where outbox table located. By default 'public'.</param>
    /// <param name="tableName">Table name. By default name of type in plural.</param>
    /// <param name="npgsqlDataSource">DbConnection datasource.</param>
    /// <param name="handlerLifetime">Which lifetime should be handler.</param>
    /// <param name="configure">Optional configure action.</param>
    /// <typeparam name="TOutboxItem">Outbox item type.</typeparam>
    /// <typeparam name="TOutboxItemHandler">Outbox item handler type.</typeparam>
    public static void AddOutboxItem<TOutboxItem, TOutboxItemHandler>(
        this IServiceCollection services,
        string? schemaName = null,
        string? tableName = null,
        NpgsqlDataSource? npgsqlDataSource = null,
        ServiceLifetime handlerLifetime = ServiceLifetime.Transient,
        Action<OutboxOptions>? configure = null
    )
        where TOutboxItem : class, IOutboxItem
        where TOutboxItemHandler : class, IOutboxItemHandler<TOutboxItem>
    {
        ArgumentNullException.ThrowIfNull(services);

        var type = typeof(TOutboxItem);
        if (_registeredTypes.Contains(type))
        {
            throw new InvalidOperationException($"The {type} already registered!");
        }

        var outboxName = type.Name;

        services
            .AddOptions<OutboxOptions>(outboxName)
            .BindConfiguration($"Outbox:{outboxName}")
            .Configure<IServiceProvider>((o, sp) =>
            {
                o.SetNpgsqlDataSource(npgsqlDataSource ?? sp.GetRequiredService<NpgsqlDataSource>());
                o.SetDbMapping(DbMapping.GetDefault(schemaName ?? "public", tableName ?? outboxName.Pluralize()));
                o.SetOutboxName(outboxName);

                o.WorkerType ??= NpgsqlWorkerTypes.COMPETING;
                configure?.Invoke(o);
            })
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.Add(ServiceDescriptor.Describe(typeof(IOutboxItemHandler<TOutboxItem>), typeof(TOutboxItemHandler), handlerLifetime));
        services.TryAddTransient<IOutboxItemHandler<TOutboxItem>, TOutboxItemHandler>();
        services.TryAddSingleton<IOutboxMaintenanceRepository<TOutboxItem>, OutboxMaintenanceItemRepository<TOutboxItem>>();

        services.TryAddKeyedTransient<IOutboxRepository<TOutboxItem>, CompetingOutboxRepository<TOutboxItem>>(NpgsqlWorkerTypes.COMPETING);
        services.TryAddKeyedTransient<IOutboxWorker, CompetingOutboxWorker>(NpgsqlWorkerTypes.COMPETING);

        services.TryAddKeyedTransient<IOutboxRepository<TOutboxItem>, StrictOrderingOutboxRepository<TOutboxItem>>(NpgsqlWorkerTypes.STRICT_ORDERING);
        services.TryAddKeyedTransient<IOutboxWorker, StrictOrderingOutboxWorker>(NpgsqlWorkerTypes.STRICT_ORDERING);

        services.TryAddSingleton(TimeProvider.System);
        services.AddHostedService<OutboxBackgroundService<TOutboxItem>>();

        _registeredTypes.Add(type);
    }

    /// <summary>
    /// Adds <typeparamref name="TOutboxItem"/> handling services.
    /// </summary>
    /// <param name="services">Service registrator.</param>
    /// <param name="schemaName">Database schema name where outbox table located. By default 'public'.</param>
    /// <param name="tableName">Table name. By default name of type in plural.</param>
    /// <param name="npgsqlDataSource">DbConnection datasource.</param>
    /// <param name="handlerLifetime">Which lifetime should be handler.</param>
    /// <param name="configure">Optional configure action.</param>
    /// <typeparam name="TOutboxItem">Outbox item type.</typeparam>
    /// <typeparam name="TOutboxItemHandler">Outbox item handler type.</typeparam>
    public static void AddOutboxItemBatch<TOutboxItem, TOutboxItemHandler>(
        this IServiceCollection services,
        string? schemaName = null,
        string? tableName = null,
        NpgsqlDataSource? npgsqlDataSource = null,
        ServiceLifetime handlerLifetime = ServiceLifetime.Transient,
        Action<OutboxOptions>? configure = null
    )
        where TOutboxItem : class, IOutboxItem
        where TOutboxItemHandler : class, IOutboxItemBatchHandler<TOutboxItem>
    {
        ArgumentNullException.ThrowIfNull(services);

        var type = typeof(TOutboxItem);
        if (_registeredTypes.Contains(type))
        {
            throw new InvalidOperationException($"The {type} already registered!");
        }

        var outboxName = type.Name;

        services
            .AddOptions<OutboxOptions>(outboxName)
            .BindConfiguration($"Outbox:{outboxName}")
            .Configure<IServiceProvider>((o, sp) =>
            {
                o.SetNpgsqlDataSource(npgsqlDataSource ?? sp.GetRequiredService<NpgsqlDataSource>());
                o.SetDbMapping(DbMapping.GetDefault(schemaName ?? "public", tableName ?? outboxName.Pluralize()));
                o.SetOutboxName(outboxName);

                o.WorkerType ??= NpgsqlWorkerTypes.BATCH_COMPETING;
                configure?.Invoke(o);
            })
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAdd(ServiceDescriptor.Describe(typeof(IOutboxItemBatchHandler<TOutboxItem>), typeof(TOutboxItemHandler), handlerLifetime));
        services.TryAddSingleton<IOutboxMaintenanceRepository<TOutboxItem>, OutboxMaintenanceItemRepository<TOutboxItem>>();

        services.TryAddKeyedTransient<IOutboxRepository<TOutboxItem>, CompetingOutboxRepository<TOutboxItem>>(NpgsqlWorkerTypes.BATCH_COMPETING);
        services.TryAddKeyedTransient<IOutboxWorker, BatchCompetingOutboxWorker>(NpgsqlWorkerTypes.BATCH_COMPETING);

        services.TryAddSingleton(TimeProvider.System);
        services.AddHostedService<OutboxBackgroundService<TOutboxItem>>();

        _registeredTypes.Add(type);
    }
}