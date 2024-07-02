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
    /// <param name="npgsqlDataSource">DbConnection datasource.</param>
    /// <param name="handlerLifetime">Which lifetime should be handler.</param>
    /// <param name="configure">Optional configure action.</param>
    /// <typeparam name="TOutboxItem">Outbox item type.</typeparam>
    /// <typeparam name="TOutboxItemHandler">Outbox item handler type.</typeparam>
    public static void AddOutboxItem<TOutboxItem, TOutboxItemHandler>(
        this IServiceCollection services,
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

        services
            .AddOptions<OutboxOptions>(type.Name)
            .BindConfiguration($"Outbox:{type.Name}")
            .Configure<IServiceProvider>((o, sp) =>
            {
                configure?.Invoke(o);

                o.SetNpgsqlDataSource(npgsqlDataSource ?? sp.GetRequiredService<NpgsqlDataSource>());
                o.SetDbMapping(DbMapping.GetDefault(o.TableName ?? type.Name.Pluralize()));

                o.WorkerType ??= NpgsqlWorkerTypes.COMPETING;
            })
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.Add(ServiceDescriptor.Describe(typeof(IOutboxItemHandler<TOutboxItem>), typeof(TOutboxItemHandler), handlerLifetime));
        services.TryAddTransient<IOutboxItemHandler<TOutboxItem>, TOutboxItemHandler>();
        services.TryAddSingleton<IOutboxRepository<TOutboxItem>, OutboxItemRepository<TOutboxItem>>();
        services.TryAddKeyedTransient<IOutboxWorker, StrictOrderingOutboxWorker>(NpgsqlWorkerTypes.STRICT_ORDERING);
        services.TryAddKeyedTransient<IOutboxWorker, CompetingOutboxWorker>(NpgsqlWorkerTypes.COMPETING);
        services.TryAddSingleton(TimeProvider.System);
        services.AddHostedService<OutboxBackgroundService<TOutboxItem>>();

        _registeredTypes.Add(type);
    }

    /// <summary>
    /// Adds <typeparamref name="TOutboxItem"/> handling services.
    /// </summary>
    /// <param name="services">Service registrator.</param>
    /// <param name="npgsqlDataSource">DbConnection datasource.</param>
    /// <param name="handlerLifetime">Which lifetime should be handler.</param>
    /// <param name="configure">Optional configure action.</param>
    /// <typeparam name="TOutboxItem">Outbox item type.</typeparam>
    /// <typeparam name="TOutboxItemHandler">Outbox item handler type.</typeparam>
    public static void AddOutboxItemBatch<TOutboxItem, TOutboxItemHandler>(
        this IServiceCollection services,
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

        services
            .AddOptions<OutboxOptions>(type.Name)
            .BindConfiguration($"Outbox:{type.Name}")
            .Configure<IServiceProvider>((o, sp) =>
            {
                configure?.Invoke(o);

                o.SetNpgsqlDataSource(npgsqlDataSource ?? sp.GetRequiredService<NpgsqlDataSource>());
                o.SetDbMapping(DbMapping.GetDefault(o.TableName ?? type.Name.Pluralize()));

                o.WorkerType ??= NpgsqlWorkerTypes.BATCH_COMPETING;
            })
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAdd(ServiceDescriptor.Describe(typeof(IOutboxItemBatchHandler<TOutboxItem>), typeof(TOutboxItemHandler), handlerLifetime));
        services.TryAddSingleton<IOutboxRepository<TOutboxItem>, OutboxItemRepository<TOutboxItem>>();
        services.TryAddKeyedTransient<IOutboxWorker, BatchCompetingOutboxWorker>(NpgsqlWorkerTypes.BATCH_COMPETING);
        services.TryAddSingleton(TimeProvider.System);
        services.AddHostedService<OutboxBackgroundService<TOutboxItem>>();

        _registeredTypes.Add(type);
    }
}