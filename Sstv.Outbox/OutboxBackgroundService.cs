using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Sstv.Outbox.Npgsql;

/// <summary>
/// Outbox worker starter.
/// </summary>
/// <typeparam name="TOutboxItem">Тип сущности.</typeparam>
internal sealed class OutboxBackgroundService<TOutboxItem> : BackgroundService
    where TOutboxItem : class, IOutboxItem
{
    private readonly IServiceProvider _sp;
    private OutboxOptions _outboxOptions;
    private readonly IDisposable? _optionsChangeTracker;
    private readonly string _outboxName;

    public OutboxBackgroundService(
        IServiceProvider sp,
        IOptionsMonitor<OutboxOptions> options
    )
    {
        ArgumentNullException.ThrowIfNull(sp);
        ArgumentNullException.ThrowIfNull(options);

        _sp = sp;
        _outboxName = typeof(TOutboxItem).Name;
        _outboxOptions = options.Get(_outboxName);
        _optionsChangeTracker = options.OnChange((outboxOptions, name) =>
        {
            if (name == _outboxName)
            {
                _outboxOptions = outboxOptions;
            }
        });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_outboxOptions.OutboxDelay);
        var worker = _sp.GetRequiredKeyedService<IOutboxWorker>(_outboxOptions.WorkerType);
        var sw = new Stopwatch();
        while (await timer.WaitForNextTickAsync(stoppingToken) && !stoppingToken.IsCancellationRequested)
        {
            sw.Restart();
            await worker.ProcessAsync<TOutboxItem>(_outboxOptions, stoppingToken).ConfigureAwait(false);
            sw.Stop();
            OutboxMetricCollector.RecordBatchProcessTime(sw.ElapsedMilliseconds, _outboxName);
        }
    }

    public override void Dispose()
    {
        _optionsChangeTracker?.Dispose();

        base.Dispose();
    }
}