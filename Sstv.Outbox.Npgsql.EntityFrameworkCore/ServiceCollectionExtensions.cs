using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Sstv.Outbox.Npgsql.EntityFrameworkCore;

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
    /// <param name="handlerLifetime">Which lifetime should be handler.</param>
    /// <param name="configure">Optional configure action.</param>
    /// <typeparam name="TDbContext">DbContext.</typeparam>
    /// <typeparam name="TOutboxItem">Outbox item type.</typeparam>
    /// <typeparam name="TOutboxItemHandler">Outbox item handler type.</typeparam>
    public static void AddOutboxItem<TDbContext, TOutboxItem, TOutboxItemHandler>(
        this IServiceCollection services,
        ServiceLifetime handlerLifetime = ServiceLifetime.Transient,
        Action<OutboxOptions>? configure = null
    )
        where TDbContext : DbContext
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
                configure?.Invoke(o);

                using var scope = sp.CreateScope();
                using var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
                o.SetDbMapping(DbMapping.GetFor<TOutboxItem>(dbContext));
                o.SetOutboxName(outboxName);

                o.WorkerType ??= EfCoreWorkerTypes.COMPETING;
            })
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.Add(ServiceDescriptor.Describe(typeof(IOutboxItemHandler<TOutboxItem>), typeof(TOutboxItemHandler),
            handlerLifetime));
        services.TryAddTransient<IOutboxItemHandler<TOutboxItem>, TOutboxItemHandler>();
        // services.TryAddSingleton<IOutboxRepository<TOutboxItem>, OutboxItemRepository<TOutboxItem>>();
        services.TryAddKeyedTransient<IOutboxWorker, StrictOrderingOutboxWorker<TDbContext>>(EfCoreWorkerTypes.STRICT_ORDERING);
        services.TryAddKeyedTransient<IOutboxWorker, CompetingOutboxWorker<TDbContext>>(EfCoreWorkerTypes.COMPETING);
        services.TryAddSingleton(TimeProvider.System);
        services.AddHostedService<OutboxBackgroundService<TOutboxItem>>();

        _registeredTypes.Add(type);
    }

    /// <summary>
    /// Adds <typeparamref name="TOutboxItem"/> handling services.
    /// </summary>
    /// <param name="services">Service registrator.</param>
    /// <param name="handlerLifetime">Which lifetime should be handler.</param>
    /// <param name="configure">Optional configure action.</param>
    /// <typeparam name="TDbContext">DbContext.</typeparam>
    /// <typeparam name="TOutboxItem">Outbox item type.</typeparam>
    /// <typeparam name="TOutboxItemHandler">Outbox item handler type.</typeparam>
    public static void AddOutboxItemBatch<TDbContext, TOutboxItem, TOutboxItemHandler>(
        this IServiceCollection services,
        ServiceLifetime handlerLifetime = ServiceLifetime.Transient,
        Action<OutboxOptions>? configure = null
    )
        where TDbContext : DbContext
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
                configure?.Invoke(o);

                using var scope = sp.CreateScope();
                using var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
                o.SetDbMapping(DbMapping.GetFor<TOutboxItem>(dbContext));
                o.SetOutboxName(outboxName);

                o.WorkerType ??= EfCoreWorkerTypes.BATCH_COMPETING;
            })
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAdd(ServiceDescriptor.Describe(typeof(IOutboxItemBatchHandler<TOutboxItem>),
            typeof(TOutboxItemHandler), handlerLifetime));
        // services.TryAddSingleton<IOutboxRepository<TOutboxItem>, OutboxItemRepository<TOutboxItem>>();
        // services.TryAddKeyedTransient<IOutboxWorker, BatchCompetingOutboxWorker>(EfCoreWorkerTypes.BATCH_COMPETING);
        services.TryAddSingleton(TimeProvider.System);
        services.AddHostedService<OutboxBackgroundService<TOutboxItem>>();

        _registeredTypes.Add(type);
    }
}