using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using Sstv.Outbox.IntegrationTests.Fixtures;
using Sstv.Outbox.Kafka;
using Sstv.Outbox.Npgsql;
using Sstv.Outbox.Sample;
using UUIDNext;

namespace Sstv.Outbox.IntegrationTests;

public sealed partial class NpgsqlOutboxWorkerTests
{
    /// <summary>
    /// Happy path of processing outbox items with priority feature.
    /// </summary>
    [Test]
    public async Task Should_publish_messages_from_outbox_with_respect_of_their_priority()
    {
        // arrange
        var spy = new NotificationMessageProducerSpy<KafkaNpgsqlOutboxItemWithPriority>();
        await using var factory = CreateWebApp(spy);

        var monitor = factory.Services.GetRequiredService<IOptionsMonitor<OutboxOptions>>();
        var options = monitor.Get(nameof(KafkaNpgsqlOutboxItemWithPriority));
        var worker = factory.Services.GetRequiredKeyedService<IOutboxWorker>(options.WorkerType);

        Assume.That(await GetCountOfOutboxItems(options), Is.Zero,
            $"Table {options.GetDbMapping().QualifiedTableName} should be empty before act");

        var expectedProcessingOrder = await SeedAsync(factory, options.OutboxItemsLimit);

        // act
        await worker.ProcessAsync<KafkaNpgsqlOutboxItemWithPriority>(options, CancellationToken.None);

        // assert
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await GetCountOfOutboxItems(options), Is.Zero,
                $"Table {options.GetDbMapping().QualifiedTableName} should be empty after act");

            Assert.That(spy.PublishedCount, Is.EqualTo(options.OutboxItemsLimit),
                "The count of published messages doesn't match with expected");

            Assert.That(spy.PublishedIds, Is.EqualTo(expectedProcessingOrder).AsCollection,
                "Published ids not matched with expected ids");
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

    private static async Task<KafkaNpgsqlOutboxItemWithPriority[]> AddItemsWithPriorityAsync(
        WebAppFactory factory,
        int priority,
        int count = 1
    )
    {
        var outboxItemFactory = factory.Services.GetRequiredService<IKafkaOutboxItemFactory<KafkaNpgsqlOutboxItemWithPriority>>();
        var monitor = factory.Services.GetRequiredService<IOptionsMonitor<OutboxOptions>>();
        var options = monitor.Get(nameof(KafkaNpgsqlOutboxItemWithPriority));

        var datasource = options.GetNpgsqlDataSource();

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

        await using var cmd = datasource.CreateCommand(
            $"""
             INSERT INTO {options.GetDbMapping().QualifiedTableName} (id, status, topic, key, value, headers, priority)
             SELECT data.id, data.status, data.topic, data.key, data.value, data.headers::jsonb, data.priority FROM unnest(@id, @status, @topic, @key, @value, @headers, @priority)
             as data(id, status, topic, key, value, headers, priority);
             """
        );

        cmd.Parameters.Add(new NpgsqlParameter<Guid[]>("id", items.Select(e => e.Id).ToArray()));
        cmd.Parameters.Add(new NpgsqlParameter<int[]>("priority", items.Select(e => e.Priority).ToArray()));
        cmd.Parameters.Add(new NpgsqlParameter<int[]>("status", items.Select(e => (int)e.Status).ToArray()));
        cmd.Parameters.Add(new NpgsqlParameter<string?[]>("topic", items.Select(e => e.Topic).ToArray()));
        cmd.Parameters.Add(new NpgsqlParameter<byte[]?[]>("key", items.Select(e => e.Key).ToArray()));
        cmd.Parameters.Add(new NpgsqlParameter<byte[]?[]>("value", items.Select(e => e.Value).ToArray()));
        cmd.Parameters.Add(new NpgsqlParameter<string[]>("headers", items.Select(e => JsonSerializer.Serialize(e.Headers)).ToArray()));

        await cmd.ExecuteNonQueryAsync();

        return items;
    }
}
