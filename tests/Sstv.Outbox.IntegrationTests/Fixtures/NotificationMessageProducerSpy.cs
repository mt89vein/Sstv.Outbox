using System.Collections.Concurrent;

namespace Sstv.Outbox.IntegrationTests.Fixtures;

/// <summary>
/// Outbox item handler spy, that collected processed message ids.
/// </summary>
internal sealed class NotificationMessageProducerSpy<T> : IOutboxItemHandler<T>
    where T : class, IKafkaOutboxItem
{
    private int _publishedCount;
    private readonly ConcurrentQueue<Guid> _concurrentQueue = new();

    public IReadOnlyList<Guid> PublishedIds => _concurrentQueue.ToArray();

    public int PublishedCount => _publishedCount;

    public Task<OutboxItemHandleResult> HandleAsync(
        T outboxItem,
        OutboxOptions options,
        CancellationToken ct = default
    )
    {
        Interlocked.Increment(ref _publishedCount);
        _concurrentQueue.Enqueue(outboxItem.Id);

        return Task.FromResult(OutboxItemHandleResult.Ok);
    }
}
