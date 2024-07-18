using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Sstv.Outbox.EntityFrameworkCore.Npgsql;
using Sstv.Outbox.IntegrationTests.Fixtures;
using Sstv.Outbox.Kafka;
using Sstv.Outbox.Sample;
using Uuid = UUIDNext.Uuid;

namespace Sstv.Outbox.IntegrationTests;

/// <summary>
/// Tests for worker with EntityFrameworkCore.
/// </summary>
[TestOf(typeof(IOutboxWorker))]
[TestOf(typeof(StrictOrderingOutboxWorker))]
[TestOf(typeof(CompetingOutboxWorker))]
[TestOf(typeof(StrictOrderingOutboxRepository<,>))]
[TestOf(typeof(CompetingOutboxRepository<,>))]
[SuppressMessage("Naming", "CA1707:Identifiers should not contain underscore symbols")]
public sealed partial class EntityFrameworkOutboxWorkerTests
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
        var spy = new NotificationMessageProducerSpy<KafkaEfOutboxItem>();
        await using var factory = CreateWebApp(spy);

        var monitor = factory.Services.GetRequiredService<IOptionsMonitor<OutboxOptions>>();
        var options = monitor.Get(nameof(KafkaEfOutboxItem));
        var worker = factory.Services.GetRequiredKeyedService<IOutboxWorker>(options.WorkerType);

        Assume.That(await GetCountOfOutboxItems(factory), Is.Zero,
            $"Table {options.GetDbMapping().QualifiedTableName} should be empty before act");

        await AddItemsAsync(factory, options.OutboxItemsLimit);

        // act
        await worker.ProcessAsync<KafkaEfOutboxItem>(options, CancellationToken.None);

        // assert
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await GetCountOfOutboxItems(factory), Is.Zero,
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
        var spy = new NotificationMessageProducerSpy<KafkaEfOutboxItem>();
        await using var factory = CreateWebApp(spy);

        var monitor = factory.Services.GetRequiredService<IOptionsMonitor<OutboxOptions>>();
        var options = monitor.Get(nameof(KafkaEfOutboxItem));
        options.WorkerType = EfCoreWorkerTypes.StrictOrdering;
        var worker = factory.Services.GetRequiredKeyedService<IOutboxWorker>(options.WorkerType);
        var expectedPublishedMessagesCount = options.OutboxItemsLimit;
        var additionalItemsInTable = 1;

        Assume.That(await GetCountOfOutboxItems(factory), Is.Zero,
            $"Table {options.GetDbMapping().QualifiedTableName} should be empty before act");

        await AddItemsAsync(factory, options.OutboxItemsLimit + additionalItemsInTable);

        // act
        await Parallel.ForAsync(0, competingWorkersCount, async (_, _) =>
            await worker.ProcessAsync<KafkaEfOutboxItem>(options, CancellationToken.None)
        );

        // assert
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await GetCountOfOutboxItems(factory), Is.EqualTo(additionalItemsInTable),
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
        var spy = new NotificationMessageProducerSpy<KafkaEfOutboxItem>();
        await using var factory = CreateWebApp(spy);

        var monitor = factory.Services.GetRequiredService<IOptionsMonitor<OutboxOptions>>();
        var options = monitor.Get(nameof(KafkaEfOutboxItem));
        options.WorkerType = EfCoreWorkerTypes.Competing;

        var worker = factory.Services.GetRequiredKeyedService<IOutboxWorker>(options.WorkerType);
        var expectedPublishedMessagesCount = options.OutboxItemsLimit * competingWorkersCount;
        var additionalItemsInTable = 1;

        Assume.That(await GetCountOfOutboxItems(factory), Is.Zero,
            $"Table {options.GetDbMapping().QualifiedTableName} should be empty before act");

        await AddItemsAsync(factory, (options.OutboxItemsLimit * competingWorkersCount) + additionalItemsInTable);

        // act
        await Parallel.ForAsync(0, competingWorkersCount, async (_, _) =>
            await worker.ProcessAsync<KafkaEfOutboxItem>(options, CancellationToken.None)
        );

        // assert
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await GetCountOfOutboxItems(factory), Is.EqualTo(additionalItemsInTable),
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
        var spy = new NotificationMessageErrorProducerMock<KafkaEfOutboxItem>(_ =>
        {
            return result ?? throw new Exception("Test configured to throw an exception");
        });
        await using var factory = CreateWebApp(spy);

        var monitor = factory.Services.GetRequiredService<IOptionsMonitor<OutboxOptions>>();
        var options = monitor.Get(nameof(KafkaEfOutboxItem));
        options.WorkerType = EfCoreWorkerTypes.Competing;
        var worker = factory.Services.GetRequiredKeyedService<IOutboxWorker>(options.WorkerType);

        Assume.That(await GetCountOfOutboxItems(factory), Is.Zero,
            $"Table {options.GetDbMapping().QualifiedTableName} should be empty before act");

        var addedItems = await AddItemsAsync(factory, options.OutboxItemsLimit);

        // act
        await worker.ProcessAsync<KafkaEfOutboxItem>(options, CancellationToken.None);

        // assert
        Assert.That(await GetCountOfOutboxItems(factory), Is.EqualTo(addedItems.Length),
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
        var spy = new NotificationMessageErrorProducerMock<KafkaEfOutboxItem>(item => ids.Contains(item.Id)
            ? OutboxItemHandleResult.Ok
            : OutboxItemHandleResult.Retry
        );
        await using var factory = CreateWebApp(spy);

        var monitor = factory.Services.GetRequiredService<IOptionsMonitor<OutboxOptions>>();
        var options = monitor.Get(nameof(KafkaEfOutboxItem));
        options.WorkerType = EfCoreWorkerTypes.StrictOrdering;
        var worker = factory.Services.GetRequiredKeyedService<IOutboxWorker>(options.WorkerType);

        Assume.That(await GetCountOfOutboxItems(factory), Is.Zero,
            $"Table {options.GetDbMapping().QualifiedTableName} should be empty before act");

        var addedItems = await AddItemsAsync(factory, options.OutboxItemsLimit);

        // first n must succeed, others - fail with exception
        const int firstNSuccess = 2;

        Assume.That(firstNSuccess, Is.LessThanOrEqualTo(options.OutboxItemsLimit));

        ids.AddRange(addedItems
            .OrderBy(x => x.Id)
            .Take(firstNSuccess)
            .Select(x => x.Id));

        // act
        await worker.ProcessAsync<KafkaEfOutboxItem>(options, CancellationToken.None);

        // assert
        Assert.That(await GetCountOfOutboxItems(factory), Is.EqualTo(addedItems.Length - firstNSuccess),
            "The count of items in table not matched with expected");
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
        var spy = new NotificationMessageErrorProducerMock<KafkaEfOutboxItem>(item => ids.Contains(item.Id)
            ? OutboxItemHandleResult.Ok
            : OutboxItemHandleResult.Retry
        );
        await using var factory = CreateWebApp(spy);

        var monitor = factory.Services.GetRequiredService<IOptionsMonitor<OutboxOptions>>();
        var options = monitor.Get(nameof(KafkaEfOutboxItem));
        options.WorkerType = EfCoreWorkerTypes.Competing;
        var worker = factory.Services.GetRequiredKeyedService<IOutboxWorker>(options.WorkerType);

        Assume.That(await GetCountOfOutboxItems(factory), Is.Zero,
            $"Table {options.GetDbMapping().QualifiedTableName} should be empty before act");

        var addedItems = await AddItemsAsync(factory, options.OutboxItemsLimit);

        // first n must succeed, others - fail with exception
        const int firstNSuccess = 2;

        Assume.That(firstNSuccess, Is.LessThanOrEqualTo(options.OutboxItemsLimit));

        ids.AddRange(addedItems
            .OrderBy(x => x.Id)
            .Take(firstNSuccess)
            .Select(x => x.Id));

        // act
        await worker.ProcessAsync<KafkaEfOutboxItem>(options, CancellationToken.None);

        // assert
        Assert.That(await GetCountOfOutboxItems(factory), Is.EqualTo(addedItems.Length - firstNSuccess),
            "The count of items in table not matched with expected");
    }

    private static WebAppFactory CreateWebApp<T>(IOutboxItemHandler<T> handler)
        where T : class, IOutboxItem
    {
        return new WebAppFactory(services =>
        {
            services.Replace(ServiceDescriptor.Singleton(handler));
        });
    }

    private static async Task<KafkaEfOutboxItem[]> AddItemsAsync(WebAppFactory factory, int count = 1)
    {
        var outboxItemFactory = factory.Services.GetRequiredService<IKafkaOutboxItemFactory<KafkaEfOutboxItem>>();
        using var scope = factory.Services.CreateScope();
        await using var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var items = Enumerable
            .Range(0, count)
            .Select(x =>
            {
                var id = Uuid.NewSequential();
                return outboxItemFactory.Create(id, new NotificationMessage(id, DateTimeOffset.UtcNow, $"My text {x}"));
            })
            .ToArray();

        ctx.KafkaEfOutboxItems.AddRange(items);
        await ctx.SaveChangesAsync();

        return items;
    }

    private static async Task<int> GetCountOfOutboxItems(WebAppFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        await using var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await ctx.KafkaEfOutboxItems.CountAsync();
    }
}
