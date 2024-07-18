using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Npgsql;
using Sstv.Outbox.IntegrationTests.Fixtures;
using Sstv.Outbox.Kafka;
using Sstv.Outbox.Npgsql;
using Sstv.Outbox.Sample;
using UUIDNext;

namespace Sstv.Outbox.IntegrationTests;

/// <summary>
/// Tests for worker with Npgsql.
/// </summary>
[TestOf(typeof(IOutboxWorker))]
[TestOf(typeof(StrictOrderingOutboxWorker))]
[TestOf(typeof(CompetingOutboxWorker))]
[TestOf(typeof(StrictOrderingOutboxRepository<>))]
[TestOf(typeof(CompetingOutboxRepository<>))]
[SuppressMessage("Naming", "CA1707:Identifiers should not contain underscore symbols")]
public sealed partial class NpgsqlOutboxWorkerTests
{
    /// <summary>
    /// Cleans db before every test.
    /// </summary>
    [SetUp]
    public Task Setup()
    {
        return SetupContainer.Database.ResetAsync();
    }

    /// <summary>
    /// Happy path. Fetch data from table and push to outbox item handler.
    /// </summary>
    [Test]
    public async Task Worker_should_process_outbox_item_table_when_process_async_called()
    {
        // arrange
        var spy = new NotificationMessageProducerSpy<KafkaNpgsqlOutboxItem>();
        await using var factory = CreateWebApp(spy);

        var monitor = factory.Services.GetRequiredService<IOptionsMonitor<OutboxOptions>>();
        var options = monitor.Get(nameof(KafkaNpgsqlOutboxItem));
        var worker = factory.Services.GetRequiredKeyedService<IOutboxWorker>(options.WorkerType);

        Assume.That(await GetCountOfOutboxItems(options), Is.Zero,
            $"Table {options.GetDbMapping().QualifiedTableName} should be empty before act");

        await AddItemsAsync(factory, options.OutboxItemsLimit);

        // act
        await worker.ProcessAsync<KafkaNpgsqlOutboxItem>(options, CancellationToken.None);

        // assert
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await GetCountOfOutboxItems(options), Is.Zero,
                $"Table {options.GetDbMapping().QualifiedTableName} should be empty after act");

            Assert.That(spy.PublishedCount, Is.EqualTo(options.OutboxItemsLimit),
                "The count of published messages doesn't match with expected");
        });
    }

    /// <summary>
    /// This test checks concurrent workers in strict ordering mode that try to fetch and process data at the same time.
    /// </summary>
    [Test]
    [TestCase(10)]
    public async Task Should_handle_concurrent_worker_requests_in_strict_ordering_mode(int competingWorkersCount)
    {
        // arrange
        var spy = new NotificationMessageProducerSpy<KafkaNpgsqlOutboxItem>();
        await using var factory = CreateWebApp(spy);

        var monitor = factory.Services.GetRequiredService<IOptionsMonitor<OutboxOptions>>();
        var options = monitor.Get(nameof(KafkaNpgsqlOutboxItem));
        options.WorkerType = NpgsqlWorkerTypes.StrictOrdering;
        var worker = factory.Services.GetRequiredKeyedService<IOutboxWorker>(options.WorkerType);
        var expectedPublishedMessagesCount = options.OutboxItemsLimit;
        var additionalItemsInTable = 1;

        Assume.That(await GetCountOfOutboxItems(options), Is.Zero,
            $"Table {options.GetDbMapping().QualifiedTableName} should be empty before act");

        await AddItemsAsync(factory, options.OutboxItemsLimit + additionalItemsInTable);

        // act
        await Parallel.ForAsync(0, competingWorkersCount, async (_, _) =>
            await worker.ProcessAsync<KafkaNpgsqlOutboxItem>(options, CancellationToken.None)
        );

        // assert
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await GetCountOfOutboxItems(options), Is.EqualTo(additionalItemsInTable),
                "Expected to be one message in the table after act");

            Assert.That(spy.PublishedCount, Is.EqualTo(expectedPublishedMessagesCount),
                "The count of published messages doesn't match with expected");
        });
    }

    /// <summary>
    /// This test checks concurrent workers in competing mode that try to fetch and process data at the same time.
    /// </summary>
    [Test]
    [TestCase(10)]
    public async Task Should_handle_concurrent_worker_requests_in_competing_mode(int competingWorkersCount)
    {
        // arrange
        var spy = new NotificationMessageProducerSpy<KafkaNpgsqlOutboxItem>();
        await using var factory = CreateWebApp(spy);

        var monitor = factory.Services.GetRequiredService<IOptionsMonitor<OutboxOptions>>();
        var options = monitor.Get(nameof(KafkaNpgsqlOutboxItem));
        options.WorkerType = NpgsqlWorkerTypes.Competing;

        var worker = factory.Services.GetRequiredKeyedService<IOutboxWorker>(options.WorkerType);
        var expectedPublishedMessagesCount = options.OutboxItemsLimit * competingWorkersCount;
        var additionalItemsInTable = 1;

        Assume.That(await GetCountOfOutboxItems(options), Is.Zero,
            $"Table {options.GetDbMapping().QualifiedTableName} should be empty before act");

        await AddItemsAsync(factory, (options.OutboxItemsLimit * competingWorkersCount) + additionalItemsInTable);

        // act
        await Parallel.ForAsync(0, competingWorkersCount, async (_, _) =>
            await worker.ProcessAsync<KafkaNpgsqlOutboxItem>(options, CancellationToken.None)
        );

        // assert
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await GetCountOfOutboxItems(options), Is.EqualTo(additionalItemsInTable),
                "The count of expected messages in the table after act not matched");

            Assert.That(spy.PublishedCount, Is.EqualTo(expectedPublishedMessagesCount),
                "The count of published messages doesn't match with expected");
        });
    }

    /// <summary>
    /// We need to retry processing of outbox item when error occured or retry result returned.
    /// </summary>
    [Test]
    [TestCase(OutboxItemHandleResult.Retry)]
    [TestCase(null)]
    public async Task Should_retry_on_error_in_competing_mode(OutboxItemHandleResult? result)
    {
        // arrange
        var spy = new NotificationMessageErrorProducerMock<KafkaNpgsqlOutboxItem>(_ =>
        {
            return result ?? throw new Exception("Test configured to throw an exception");
        });
        await using var factory = CreateWebApp(spy);

        var monitor = factory.Services.GetRequiredService<IOptionsMonitor<OutboxOptions>>();
        var options = monitor.Get(nameof(KafkaNpgsqlOutboxItem));
        options.WorkerType = NpgsqlWorkerTypes.Competing;
        var worker = factory.Services.GetRequiredKeyedService<IOutboxWorker>(options.WorkerType);

        Assume.That(await GetCountOfOutboxItems(options), Is.Zero,
            $"Table {options.GetDbMapping().QualifiedTableName} should be empty before act");

        var addedItems = await AddItemsAsync(factory, options.OutboxItemsLimit);

        // act
        await worker.ProcessAsync<KafkaNpgsqlOutboxItem>(options, CancellationToken.None);

        // assert
        Assert.That(await GetCountOfOutboxItems(options), Is.EqualTo(addedItems.Length),
            "All messages should be in table after act");
    }

    /// <summary>
    /// Test that covers case when batch partially processed and error occur.
    /// Processed items should commited, others should retried without reorderings.
    /// </summary>
    [Test]
    public async Task Should_stop_process_batch_when_first_failure_in_strict_order_mode()
    {
        // arrange
        var ids = new List<Guid>();
        var mock = new NotificationMessageErrorProducerMock<KafkaNpgsqlOutboxItem>(item => ids.Contains(item.Id)
            ? OutboxItemHandleResult.Ok
            : OutboxItemHandleResult.Retry
        );
        await using var factory = CreateWebApp(mock);

        var monitor = factory.Services.GetRequiredService<IOptionsMonitor<OutboxOptions>>();
        var options = monitor.Get(nameof(KafkaNpgsqlOutboxItem));
        options.WorkerType = NpgsqlWorkerTypes.StrictOrdering;
        var worker = factory.Services.GetRequiredKeyedService<IOutboxWorker>(options.WorkerType);

        Assume.That(await GetCountOfOutboxItems(options), Is.Zero,
            $"Сообщений в таблице {options.GetDbMapping().QualifiedTableName} быть не должно");

        var addedItems = await AddItemsAsync(factory, options.OutboxItemsLimit);

        // первые N элементов будут обработаны успешно, а на последующие в батче - ошибка
        const int firstNSuccess = 2;

        Assume.That(firstNSuccess, Is.LessThanOrEqualTo(options.OutboxItemsLimit));

        ids.AddRange(addedItems
            .OrderBy(x => x.Id)
            .Take(firstNSuccess)
            .Select(x => x.Id));

        // act
        await worker.ProcessAsync<KafkaNpgsqlOutboxItem>(options, CancellationToken.None);

        // assert
        Assert.That(await GetCountOfOutboxItems(options),
            Is.EqualTo(addedItems.Length - firstNSuccess),
            "В БД должны остаться только необработанные");
    }

    /// <summary>
    /// Test that covers case when batch partially processed and error occur.
    /// Processed items should commited, others should retried without reorderings.
    /// </summary>
    [Test]
    public async Task Should_stop_batch_processing_and_retry_failed_item_when_first_failure_in_competing_mode()
    {
        // arrange
        var ids = new List<Guid>();
        var mock = new NotificationMessageErrorProducerMock<KafkaNpgsqlOutboxItem>(item => ids.Contains(item.Id)
            ? OutboxItemHandleResult.Ok
            : OutboxItemHandleResult.Retry
        );
        await using var factory = CreateWebApp(mock);

        var monitor = factory.Services.GetRequiredService<IOptionsMonitor<OutboxOptions>>();
        var options = monitor.Get(nameof(KafkaNpgsqlOutboxItem));
        options.WorkerType = NpgsqlWorkerTypes.Competing;
        var worker = factory.Services.GetRequiredKeyedService<IOutboxWorker>(options.WorkerType);

        Assume.That(await GetCountOfOutboxItems(options), Is.Zero,
            $"Сообщений в таблице {options.GetDbMapping().QualifiedTableName} быть не должно");

        var addedItems = await AddItemsAsync(factory, options.OutboxItemsLimit);

        // первые N элементов будут обработаны успешно, а на последующие в батче - ошибка
        const int firstNSuccess = 2;

        Assume.That(firstNSuccess, Is.LessThanOrEqualTo(options.OutboxItemsLimit));

        ids.AddRange(addedItems
            .OrderBy(x => x.Id)
            .Take(firstNSuccess)
            .Select(x => x.Id));

        // act
        await worker.ProcessAsync<KafkaNpgsqlOutboxItem>(options, CancellationToken.None);

        // assert
        Assert.That(await GetCountOfOutboxItems(options),
            Is.EqualTo(addedItems.Length - firstNSuccess),
            "Все сообщения должны остаться, так как всегда отдаем ошибку");
    }

    private static WebAppFactory CreateWebApp<T>(IOutboxItemHandler<T> handler)
        where T : class, IOutboxItem
    {
        return new WebAppFactory(services =>
        {
            services.Replace(ServiceDescriptor.Singleton(handler));
        });
    }

    private static async Task<KafkaNpgsqlOutboxItem[]> AddItemsAsync(WebAppFactory factory, int count = 1)
    {
        var outboxItemFactory = factory.Services.GetRequiredService<IKafkaOutboxItemFactory<KafkaNpgsqlOutboxItem>>();
        var monitor = factory.Services.GetRequiredService<IOptionsMonitor<OutboxOptions>>();
        var options = monitor.Get(nameof(KafkaNpgsqlOutboxItem));

        var datasource = options.GetNpgsqlDataSource();

        var items = Enumerable
            .Range(0, count)
            .Select(x =>
            {
                var id = Uuid.NewSequential();
                return outboxItemFactory.Create(id, new NotificationMessage(id, DateTimeOffset.UtcNow, $"My text {x}"));
            })
            .ToArray();

        await using var cmd = datasource.CreateCommand(
            $"""
             INSERT INTO {options.GetDbMapping().QualifiedTableName} (id, status, topic, key, value, headers)
             SELECT data.id, data.status, data.topic, data.key, data.value, data.headers::jsonb FROM unnest(@id, @status, @topic, @key, @value, @headers)
             as data(id, status, topic, key, value, headers);
             """
        );

        cmd.Parameters.Add(new NpgsqlParameter<Guid[]>("id", items.Select(e => e.Id).ToArray()));
        cmd.Parameters.Add(new NpgsqlParameter<int[]>("status", items.Select(e => (int)e.Status).ToArray()));
        cmd.Parameters.Add(new NpgsqlParameter<string?[]>("topic", items.Select(e => e.Topic).ToArray()));
        cmd.Parameters.Add(new NpgsqlParameter<byte[]?[]>("key", items.Select(e => e.Key).ToArray()));
        cmd.Parameters.Add(new NpgsqlParameter<byte[]?[]>("value", items.Select(e => e.Value).ToArray()));
        cmd.Parameters.Add(new NpgsqlParameter<string[]>("headers", items.Select(e => JsonSerializer.Serialize(e.Headers)).ToArray()));

        await cmd.ExecuteNonQueryAsync();

        return items;
    }

    private static async Task<int> GetCountOfOutboxItems(OutboxOptions options)
    {
        var datasource = options.GetNpgsqlDataSource();

        await using var connection = datasource.CreateConnection();

        return await connection.QuerySingleAsync<int>($"SELECT COUNT(*) FROM {options.GetDbMapping().QualifiedTableName}");
    }
}
