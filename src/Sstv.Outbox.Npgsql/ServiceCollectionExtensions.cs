using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Sstv.Outbox.Features.Partitions;

namespace Sstv.Outbox.Npgsql;

/// <summary>
/// Extensions methods for <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds <typeparamref name="TOutboxItem"/> handling services.
    /// </summary>
    /// <param name="services">Service registrator.</param>
    /// <param name="schemaName">Database schema name where outbox table located. By default, 'public'.</param>
    /// <param name="tableName">Table name. By default, name of type in plural.</param>
    /// <param name="npgsqlDataSource">DbConnection datasource.</param>
    /// <param name="configure">Optional configure action.</param>
    /// <typeparam name="TOutboxItem">Outbox item type.</typeparam>
    public static OutboxItemHandlerBuilder<TOutboxItem> AddOutboxItem<TOutboxItem>(
        this IServiceCollection services,
        string? schemaName = null,
        string? tableName = null,
        NpgsqlDataSource? npgsqlDataSource = null,
        Action<OutboxOptions>? configure = null
    )
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
                o.SetNpgsqlDataSource(npgsqlDataSource ?? sp.GetRequiredService<NpgsqlDataSource>());
                o.SetDbMapping(DbMapping.GetDefault(schemaName ?? "public", tableName ?? outboxName.Pluralize()));
                o.SetPriorityFeature<TOutboxItem>();
                o.SetStatusFeature<TOutboxItem>();

                o.WorkerType ??= NpgsqlWorkerTypes.Competing;
                configure?.Invoke(o);
            })
            .ValidateDataAnnotations()
            .Validate(validation:
                options => typeof(TOutboxItem).IsAssignableTo(typeof(IHasStatus)) ||
                           options.WorkerType is not (NpgsqlWorkerTypes.Competing or NpgsqlWorkerTypes.BatchCompeting),
                failureMessage:
                $"You should implement {typeof(IHasStatus)} in your {typeof(TOutboxItem)} when worker type in competing mode"
            )
            .Validate(validation:
                options => (options.PartitionSettings.Enabled && typeof(TOutboxItem).IsAssignableTo(typeof(IHasStatus))) || !options.PartitionSettings.Enabled,
                failureMessage:
                $"You should implement {typeof(IHasStatus)} in your {typeof(TOutboxItem)} when partitions enabled"
            )
            .ValidateOnStart();

        services.TryAddKeyedTransient<IOutboxRepository<TOutboxItem>, StrictOrderingOutboxRepository<TOutboxItem>>(NpgsqlWorkerTypes.StrictOrdering);
        services.TryAddKeyedTransient<IOutboxWorker, StrictOrderingOutboxWorker>(NpgsqlWorkerTypes.StrictOrdering);

        services.TryAddKeyedTransient<IOutboxRepository<TOutboxItem>, StrictOrderingOutboxRepository<TOutboxItem>>(NpgsqlWorkerTypes.BatchStrictOrdering);
        services.TryAddKeyedTransient<IOutboxWorker, BatchStrictOrderingOutboxWorker>(NpgsqlWorkerTypes.BatchStrictOrdering);

        services.TryAddKeyedTransient<IOutboxRepository<TOutboxItem>, CompetingOutboxRepository<TOutboxItem>>(NpgsqlWorkerTypes.Competing);
        services.TryAddKeyedTransient<IOutboxWorker, CompetingOutboxWorker>(NpgsqlWorkerTypes.Competing);

        services.TryAddKeyedTransient<IOutboxRepository<TOutboxItem>, CompetingOutboxRepository<TOutboxItem>>(NpgsqlWorkerTypes.BatchCompeting);
        services.TryAddKeyedTransient<IOutboxWorker, BatchCompetingOutboxWorker>(NpgsqlWorkerTypes.BatchCompeting);

        services.TryAddSingleton(TimeProvider.System);
        services.AddHostedService<OutboxBackgroundService<TOutboxItem>>();

        services.AddHostedService<OutboxPartitionerBackgroundService<TOutboxItem>>();
        services.TryAddSingleton<IPartitioner<TOutboxItem>, Partitioner<TOutboxItem>>();

        services.TryAddSingleton<IOutboxMaintenanceRepository<TOutboxItem>, OutboxMaintenanceItemRepository<TOutboxItem>>();

        return new OutboxItemHandlerBuilder<TOutboxItem>(services, outboxName);
    }
}
