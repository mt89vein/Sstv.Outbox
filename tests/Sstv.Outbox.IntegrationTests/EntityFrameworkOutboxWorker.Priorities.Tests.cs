using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sstv.Outbox.EntityFrameworkCore.Npgsql;
using Sstv.Outbox.IntegrationTests.Fixtures;
using Sstv.Outbox.Kafka;
using Sstv.Outbox.Sample;
using UUIDNext;

namespace Sstv.Outbox.IntegrationTests;

public sealed partial class EntityFrameworkOutboxWorkerTests
{
    /// <summary>
    /// Тест на обработку сообщений из таблицы с учетом приоритетов.
    /// </summary>
    [Test]
    public async Task ShouldPublishMessagesFromOutboxWithRespectOfTheirPriority()
    {
        // arrange
        var spy = new NotificationMessageProducerSpy<KafkaEfOutboxItemWithPriority>();
        await using var factory = CreateWebApp(spy);

        var monitor = factory.Services.GetRequiredService<IOptionsMonitor<OutboxOptions>>();
        var options = monitor.Get(nameof(KafkaEfOutboxItemWithPriority));
        var worker = factory.Services.GetRequiredKeyedService<IOutboxWorker>(options.WorkerType);

        Assume.That(await GetCountOfOutboxItemsWithPriority(factory), Is.Zero,
            $"Сообщений в таблице {options.GetDbMapping().QualifiedTableName} быть не должно");

        var expectedProcessingOrder = await SeedAsync(factory, options.OutboxItemsLimit);

        // act
        await worker.ProcessAsync<KafkaEfOutboxItemWithPriority>(options, CancellationToken.None);

        // assert
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await GetCountOfOutboxItemsWithPriority(factory), Is.Zero,
                $"Сообщений в таблице {options.GetDbMapping().QualifiedTableName} быть не должно");

            Assert.That(spy.PublishedCount, Is.EqualTo(options.OutboxItemsLimit),
                "Количество опубликованных сообщений не совпадает");

            Assert.That(spy.PublishedIds, Is.EqualTo(expectedProcessingOrder).AsCollection);
        });

        static async Task<Guid[]> SeedAsync(WebAppFactory factory, int limit)
        {
            var itemsWithFirstPriority = await AddItemsWithPriorityAsync(factory, 1, 1);

            var itemsWithSecondPriority = await AddItemsWithPriorityAsync(factory, 8, 1);

            var itemsWithHighestPriority = await AddItemsWithPriorityAsync(factory, 55, limit - 2);

            return itemsWithHighestPriority
                .Concat(itemsWithSecondPriority)
                .Concat(itemsWithFirstPriority)
                .Select(x => x.Id)
                .ToArray();
        }
    }

    private static async Task<KafkaEfOutboxItemWithPriority[]> AddItemsWithPriorityAsync(
        WebAppFactory factory,
        int priority,
        int count = 1
    )
    {
        var outboxItemFactory = factory.Services.GetRequiredService<IKafkaOutboxItemFactory<KafkaEfOutboxItemWithPriority>>();
        using var scope = factory.Services.CreateScope();
        await using var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var items = Enumerable
            .Range(0, count)
            .Select(x =>
            {
                var id = Uuid.NewSequential();
                var item = outboxItemFactory.Create(id, new NotificationMessage(id, DateTimeOffset.UtcNow, $"My text {x}"));
                item.Priority = priority;

                return item;
            })
            .ToArray();

        ctx.KafkaEfOutboxItemWithPriorities.AddRange(items);
        await ctx.SaveChangesAsync();

        return items;
    }

    private static async Task<int> GetCountOfOutboxItemsWithPriority(WebAppFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        await using var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await ctx.KafkaEfOutboxItemWithPriorities.CountAsync();
    }
}
