namespace Sstv.Outbox.IntegrationTests.Fixtures;

/// <summary>
/// Стаб для Kafka Producer, который выкидывает или возвращает ошибку.
/// </summary>
internal sealed class NotificationMessageErrorProducerMock<T> : IOutboxItemHandler<T>
    where T : class, IKafkaOutboxItem
{
    private readonly Func<T, OutboxItemHandleResult> _result;

    public NotificationMessageErrorProducerMock(Func<T, OutboxItemHandleResult> result)
    {
        _result = result;
    }

    public Task<OutboxItemHandleResult> HandleAsync(
        T outboxItem,
        OutboxOptions options,
        CancellationToken ct = default
    )
    {
        return Task.FromResult(_result(outboxItem));
    }
}
