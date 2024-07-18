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
/// Тесты на воркер outbox.
/// </summary>
[TestOf(typeof(IOutboxWorker))]
[TestOf(typeof(StrictOrderingOutboxWorker))]
[TestOf(typeof(CompetingOutboxWorker))]
[TestOf(typeof(StrictOrderingOutboxRepository<,>))]
[TestOf(typeof(CompetingOutboxRepository<,>))]
public sealed partial class EntityFrameworkOutboxWorkerTests
{
    /// <summary>
    /// Очищает БД перед каждым тестом.
    /// </summary>
    [SetUp]
    public Task Setup()
    {
        return SetupContainer.Database.ResetAsync();
    }

    /// <summary>
    /// Тест на стандартную обработку - получили сообщения из таблицы, отправили в обработку.
    /// </summary>
    [Test]
    public async Task ShouldPublishMessagesFromOutbox()
    {
        // arrange
        var spy = new NotificationMessageProducerSpy<KafkaEfOutboxItem>();
        await using var factory = CreateWebApp(spy);

        var monitor = factory.Services.GetRequiredService<IOptionsMonitor<OutboxOptions>>();
        var options = monitor.Get(nameof(KafkaEfOutboxItem));
        var worker = factory.Services.GetRequiredKeyedService<IOutboxWorker>(options.WorkerType);

        Assume.That(await GetCountOfOutboxItems(factory), Is.Zero,
            $"Сообщений в таблице {options.GetDbMapping().QualifiedTableName} быть не должно");

        await AddItemsAsync(factory, options.OutboxItemsLimit);

        // act
        await worker.ProcessAsync<KafkaEfOutboxItem>(options, CancellationToken.None);

        // assert
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await GetCountOfOutboxItems(factory), Is.Zero,
                $"Сообщений в таблице {options.GetDbMapping().QualifiedTableName} быть не должно");

            Assert.That(spy.PublishedCount, Is.EqualTo(options.OutboxItemsLimit),
                "Количество опубликованных сообщений не совпадает");
        });
    }

    /// <summary>
    /// Тест при котором проверяется конкурентная работа нескольких воркеров одновременно
    /// в режиме strict_ordering.
    /// </summary>
    [Test]
    [TestCase(10)]
    public async Task ShouldHandleConcurrentWorkerRequestsInStrictOrderingMode(int competingWorkersCount)
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
            $"Сообщений в таблице {options.GetDbMapping().QualifiedTableName} быть не должно");

        await AddItemsAsync(factory, options.OutboxItemsLimit + additionalItemsInTable);

        // act
        await Parallel.ForAsync(0, competingWorkersCount, async (_, _) =>
            await worker.ProcessAsync<KafkaEfOutboxItem>(options, CancellationToken.None)
        );

        // assert
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await GetCountOfOutboxItems(factory), Is.EqualTo(additionalItemsInTable),
                "Должно быть одно сообщение, так как работает только 1 воркер");

            Assert.That(spy.PublishedCount, Is.EqualTo(expectedPublishedMessagesCount),
                "Количество опубликованных сообщений не совпадает");
        });
    }

    /// <summary>
    /// Тест при котором проверяется конкурентная работа нескольких воркеров одновременно
    /// в режиме competing.
    /// </summary>
    [Test]
    [TestCase(10)]
    public async Task ShouldHandleConcurrentWorkerRequestsInCompetingMode(int competingWorkersCount)
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
            $"Сообщений в таблице {options.GetDbMapping().QualifiedTableName} быть не должно");

        await AddItemsAsync(factory, (options.OutboxItemsLimit * competingWorkersCount) + additionalItemsInTable);

        // act
        await Parallel.ForAsync(0, competingWorkersCount, async (_, _) =>
            await worker.ProcessAsync<KafkaEfOutboxItem>(options, CancellationToken.None)
        );

        // assert
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await GetCountOfOutboxItems(factory), Is.EqualTo(additionalItemsInTable),
                "Количество остаточных сообщений не совпадает");

            Assert.That(spy.PublishedCount, Is.EqualTo(expectedPublishedMessagesCount),
                "Количество опубликованных сообщений не совпадает");
        });
    }

    /// <summary>
    /// Тест, который обрабатывает ретраи в случае ошибок в обработчике.
    /// </summary>
    [Test]
    [TestCase(OutboxItemHandleResult.Retry)]
    [TestCase(null)]
    public async Task ShouldRetryOnErrorInCompetingMode(OutboxItemHandleResult? result)
    {
        // arrange
        var spy = new NotificationMessageErrorProducerMock<KafkaEfOutboxItem>(_ =>
        {
            return result ?? throw new Exception("Тестом заложено, что не удалось опубликовать");
        });
        await using var factory = CreateWebApp(spy);

        var monitor = factory.Services.GetRequiredService<IOptionsMonitor<OutboxOptions>>();
        var options = monitor.Get(nameof(KafkaEfOutboxItem));
        options.WorkerType = EfCoreWorkerTypes.Competing;
        var worker = factory.Services.GetRequiredKeyedService<IOutboxWorker>(options.WorkerType);

        Assume.That(await GetCountOfOutboxItems(factory), Is.Zero,
            $"Сообщений в таблице {options.GetDbMapping().QualifiedTableName} быть не должно");

        var addedItems = await AddItemsAsync(factory, options.OutboxItemsLimit);

        // act
        await worker.ProcessAsync<KafkaEfOutboxItem>(options, CancellationToken.None);

        // assert
        Assert.That(await GetCountOfOutboxItems(factory), Is.EqualTo(addedItems.Length),
            "Все сообщения должны остаться, так как всегда отдаем ошибку");
    }

    /// <summary>
    /// Тест, который проверяет, что если происходит ошибка во время батча
    /// то уже обработанные будут удалены из бд.
    /// </summary>
    [Test]
    public async Task ShouldStopProcessBatchOnFirstFailureInStrictOrderMode()
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
        await worker.ProcessAsync<KafkaEfOutboxItem>(options, CancellationToken.None);

        // assert
        Assert.That(await GetCountOfOutboxItems(factory), Is.EqualTo(addedItems.Length - firstNSuccess),
            "В БД должны остаться только необработанные");
    }

    /// <summary>
    /// Тест, который проверяет, что если происходит ошибка во время батча
    /// то уже обработанные будут удалены из бд. А тот что не удалось - будет отправлен на ретрай.
    /// </summary>
    [Test]
    public async Task ShouldStopBatchProcessingAndRetryFailedItemOnFirstFailureInCompetingMode()
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
        await worker.ProcessAsync<KafkaEfOutboxItem>(options, CancellationToken.None);

        // assert
        Assert.That(await GetCountOfOutboxItems(factory), Is.EqualTo(addedItems.Length - firstNSuccess),
            "В БД должны остаться только необработанные");
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
