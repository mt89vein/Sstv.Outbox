using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Sstv.Outbox.EntityFrameworkCore.Npgsql;

/// <summary>
/// Extensions methods for <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds <typeparamref name="TOutboxItem"/> handling services.
    /// </summary>
    /// <param name="services">Service registrator.</param>
    /// <param name="configure">Optional configure action.</param>
    /// <typeparam name="TDbContext">DbContext.</typeparam>
    /// <typeparam name="TOutboxItem">Outbox item type.</typeparam>
    public static OutboxItemHandlerBuilder<TOutboxItem> AddOutboxItem<TDbContext, TOutboxItem>(
        this IServiceCollection services,
        Action<OutboxOptions>? configure = null
    )
        where TDbContext : DbContext
        where TOutboxItem : class, IOutboxItem
    {
        ArgumentNullException.ThrowIfNull(services);

        var type = typeof(TOutboxItem);

        if (services.Contains(ServiceDescriptor.Singleton<IHostedService, OutboxBackgroundService<TOutboxItem>>()))
        {
            throw new InvalidOperationException($"The {type} already has been registered!");
        }

        var outboxName = type.Name;

        services
            .AddOptions<OutboxOptions>(outboxName)
            .BindConfiguration($"Outbox:{outboxName}")
            .Configure<IServiceProvider>((o, sp) =>
            {
                o.SetOutboxName(outboxName);
                using var scope = sp.CreateScope();
                using var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
                o.SetDbMapping(DbMapping.GetFor<TOutboxItem>(dbContext));
                o.SetPriorityFeature<TOutboxItem>();

                o.WorkerType ??= EfCoreWorkerTypes.Competing;
                configure?.Invoke(o);
            })
            .ValidateDataAnnotations()
            .Validate(validation:
                options => typeof(TOutboxItem).IsAssignableTo(typeof(IHasStatus)) ||
                           options.WorkerType is not (EfCoreWorkerTypes.Competing or EfCoreWorkerTypes.BatchCompeting),
                failureMessage:
                $"You should implement {typeof(IHasStatus)} in your {typeof(TOutboxItem)} when worker type in competing mode"
            ).ValidateOnStart();

        services.TryAddScoped<IOutboxMaintenanceRepository<TOutboxItem>, OutboxMaintenanceItemRepository<TDbContext, TOutboxItem>>();

        services.TryAddKeyedTransient<IOutboxRepository<TOutboxItem>, StrictOrderingOutboxRepository<TDbContext, TOutboxItem>>(EfCoreWorkerTypes.StrictOrdering);
        services.TryAddKeyedTransient<IOutboxWorker, StrictOrderingOutboxWorker>(EfCoreWorkerTypes.StrictOrdering);

        services.TryAddKeyedTransient<IOutboxRepository<TOutboxItem>, StrictOrderingOutboxRepository<TDbContext, TOutboxItem>>(EfCoreWorkerTypes.BatchStrictOrdering);
        services.TryAddKeyedTransient<IOutboxWorker, BatchStrictOrderingOutboxWorker>(EfCoreWorkerTypes.BatchStrictOrdering);

        services.TryAddKeyedTransient<IOutboxRepository<TOutboxItem>, CompetingOutboxRepository<TDbContext, TOutboxItem>>(EfCoreWorkerTypes.Competing);
        services.TryAddKeyedTransient<IOutboxWorker, CompetingOutboxWorker>(EfCoreWorkerTypes.Competing);

        services.TryAddKeyedTransient<IOutboxRepository<TOutboxItem>, CompetingOutboxRepository<TDbContext, TOutboxItem>>(EfCoreWorkerTypes.BatchCompeting);
        services.TryAddKeyedTransient<IOutboxWorker, BatchCompetingOutboxWorker>(EfCoreWorkerTypes.BatchCompeting);

        services.TryAddSingleton(TimeProvider.System);
        services.AddHostedService<OutboxBackgroundService<TOutboxItem>>();

        return new OutboxItemHandlerBuilder<TOutboxItem>(services, outboxName);
    }
}
