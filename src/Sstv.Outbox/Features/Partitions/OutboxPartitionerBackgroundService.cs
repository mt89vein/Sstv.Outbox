using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Sstv.Outbox.Features.Partitions;

namespace Sstv.Outbox;

/// <summary>
/// Outbox partitioner starter.
/// </summary>
/// <typeparam name="TOutboxItem">Тип сущности.</typeparam>
internal sealed class OutboxPartitionerBackgroundService<TOutboxItem> : BackgroundService
    where TOutboxItem : class, IOutboxItem
{
    private readonly IServiceProvider _sp;
    private readonly OutboxOptions _outboxOptions;

    public OutboxPartitionerBackgroundService(
        IServiceProvider sp,
        IOptionsMonitor<OutboxOptions> options
    )
    {
        ArgumentNullException.ThrowIfNull(sp);
        ArgumentNullException.ThrowIfNull(options);

        _sp = sp;
        _outboxOptions = options.Get(typeof(TOutboxItem).Name);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_outboxOptions.PartitionSettings.Enabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(_outboxOptions.PartitionSettings.PrecreatePartitionPeriod);

        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _sp.CreateScope();
            var partitioner = scope.ServiceProvider.GetRequiredService<IPartitioner<TOutboxItem>>();
            await partitioner.CreatePartitionsAsync(stoppingToken);
            await partitioner.DeleteOldPartitionsAsync(stoppingToken);
            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }
}
