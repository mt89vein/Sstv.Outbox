Sstv.Outbox
========

![Pipeline](https://github.com/mt89vein/Sstv.Outbox/workflows/publish/badge.svg)
[![Coverage Status](https://coveralls.io/repos/github/mt89vein/Sstv.Outbox/badge.svg?branch=master)](https://coveralls.io/github/mt89vein/Sstv.Outbox?branch=master)

Sstv.Outbox is the set of libraries that implements [Transactional Outbox Pattern](https://microservices.io/patterns/data/transactional-outbox.html).
It contains several abstractions that provide ability to change processing behavior.

This library can be used not only for producing messages for Kafka, but you can also make HTTP calls or whatever else.
The library has extensibility points. You can replace almost any functional part.

This repository contains several NuGet packages:

| Package                                                                                | Version                                                                                                                                                                                | Description                                                       |
|----------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|-------------------------------------------------------------------|
| [Sstv.Outbox](./src/Sstv.Outbox)                                                       | [![NuGet version](https://img.shields.io/nuget/v/Sstv.Outbox.svg?style=flat-square)](https://www.nuget.org/packages/Sstv.Outbox)                                                       | Core lib that contains abstractions and some base implementations |
| [Sstv.Outbox.EntityFrameworkCore.Npgsql](./src/Sstv.Outbox.EntityFrameworkCore.Npgsql) | [![NuGet version](https://img.shields.io/nuget/v/Sstv.Outbox.EntityFrameworkCore.Npgsql.svg?style=flat-square)](https://www.nuget.org/packages/Sstv.Outbox.EntityFrameworkCore.Npgsql) | Implementation using EntityFrameworkCore.Npgsql                   |
| [Sstv.Outbox.Npgsql](./src/Sstv.Outbox.Npgsql)                                         | [![NuGet version](https://img.shields.io/nuget/v/Sstv.Outbox.Npgsql.svg?style=flat-square)](https://www.nuget.org/packages/Sstv.Outbox.Npgsql)                                         | Implementation using Npgsql                                       |
| [Sstv.Outbox.Kafka](./Sstv.Outbox.Kafka)                                               | [![NuGet version](https://img.shields.io/nuget/v/Sstv.Outbox.Kafka.svg?style=flat-square)](https://www.nuget.org/packages/Sstv.Outbox.Kafka)                                           | OutboxItemHandler implementation for producing to Kafka           |

### Why?

Often we need to write something to the database and make an external call (send an HTTP request, publish a message to the broker, etc.).
Any of these operations may fail. If you write to the database but an external call fails, you need to do infinity retries (which is not an option) to ensure data consistency.
If you choose to do an external call first, and when it succeeds, write to the database, which may also fail.

You can also open a transaction, send your changes to the database, make an external call, and when it succeeds, commit the transaction.
But this has a drawback: transactions should be as short as possible because they slow down database internal mechanisms cause of MVCC.

### How it works?

You need to create an outbox table where outgoing events/data will be stored.
Events/data have to be written to the table as a single transaction with business changes.

This table will be periodically scanned by background workers for new records. Then fetched data processed - make an external call or whatever else. When it succeeds, the record will be deleted from the outbox table.
Super simple, isn't it?

### How to use it?

First of all you need to install NuGet package.

```ps1
dotnet add Sstv.Outbox.EntityFrameworkCore.Npgsql or Sstv.Outbox.Npgsql

-- if need kafka:
dotnet add Sstv.Outbox.Kafka
```

or
```xml
<ItemGroup>
    <PackageReference Include="Sstv.Outbox.EntityFrameworkCore.Npgsql" Version="1.0.0" />
    <PackageReference Include="Sstv.Outbox.Npgsql" Version="1.0.0" />
    <PackageReference Include="Sstv.Outbox.Kafka" Version="1.0.0" />
</ItemGroup>
```

Full example of usage you can see [here](./tests/Sstv.Outbox.Sample).

### Example of publishing data to Kafka using Confluent.Kafka and EntityFrameworkCore

```csharp
public class NotificationMessageOutboxItem : IKafkaOutboxItem
{
    public Guid Id { get; init }
    
    // other fields omited for brevity
}

// Add DbSet to DbContext: 
internal sealed class ApplicationContext : DbContext
{
    public DbSet<NotificationMessageOutboxItem> NotificationMessageOutboxItems { get; set; } = null!;
}

// Configure table
internal sealed class NotificationMessageOutboxItemConfiguration : IEntityTypeConfiguration<NotificationMessageOutboxItem>
{
    public void Configure(EntityTypeBuilder<NotificationMessageOutboxItem> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(x => x.Id);
        builder
            .Property(x => x.Id)
            .ValueGeneratedNever();

        builder
            .Property(x => x.Headers)
            .HasColumnType("json");
    }
}

// Register to DI:
services
    .AddOutboxItem<ApplicationContext, NotificationMessageOutboxItem>()
    .WithKafkaProducer<OutboxKafkaHandler<TOutboxItem>, TOutboxItem, Guid, NotificationMessage>(
        new KafkaTopicConfig<Guid, NotificationMessage>
        {
            DefaultTopicName = "notification-messages",
            KeyDeserializer = new UuidBinarySerializer(),
            KeySerializer = new UuidBinarySerializer(),
            ValueDeserializer = new SystemTextJsonSerializer<NotificationMessage>(),
            ValueSerializer = new SystemTextJsonSerializer<NotificationMessage>(),
            
            // provide here IProducer with your configuration.
            Producer = new ProducerBuilder<byte[]?, byte[]?>(new ProducerConfig
            {
                SecurityProtocol = SecurityProtocol.Plaintext,
                BootstrapServers = "localhost:9092"
            }).Build()
        });
```

To send some data, we need to write it to our outbox table first.
To make it easier, you can inject IKafkaOutboxItemFactory<TOutboxItem> and call the Create method.
Add the created OutboxItem to dbContext and save the changes:

```csharp

public class NotificationPublisher
{
    private readonly IKafkaOutboxItemFactory<NotificationMessageOutboxItem> _factory;
    private readonly ApplicationDbContext _ctx;

    public NotificationPublisher(IKafkaOutboxItemFactory<NotificationMessageOutboxItem> factory, ApplicationDbContext ctx)
    {
        _factory = factory;
        _ctx = ctx;
    }

    public void Notify(NotificationMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var item = _factory.Create(
            key: message.Id,
            value: message
        );

        _ctx.NotificationMessageOutboxItems.Add(item);
    }
}
```

Doing so, we create a record in the outbox table, and the background worker, using IProducer, will send it to the Kafka topic.

### How to configure OutboxItem behavior

You can provide a lambda into the AddOutboxItem method for configuring settings:



**WorkerType** - Use this setting for selecting worker type e.g. ef_strict_ordering or ef_competing. Or if you want you can provide your own implementation. Reacts to changes in runtime.

**IsWorkerEnabled** - Use this to start or stop worker. Reacts to changes in runtime.

**OutboxItemsLimit** - How many items fetched from database per worker cycle. Reacts to changes in runtime.

**OutboxDelay** - How much time should worker sleep between batch processing. If batch processed longer than this delay, worker may be called immediately.

**RetrySettings** - Here you can configure retry policy. It make sense if you OutboxItem implements IHasStatus interface.

**NextGuid** - This is lambda for generating uuid v7 instead of default from [UUIDNext](https://github.com/mareek/UUIDNext)

If you want, you can also use appsettings.json. It binds to the configuration section by type name:

```json
{
    "Outbox": {
        "NotificationMessageOutboxItem": {
            "IsWorkerEnabled": true,
            "OutboxItemsLimit": 10,
            "WorkerType": "ef_competing",
            "WorkerDelay": "00:00:05"
        },
        "other": {  }
    }
}
```

That's all. Configuration completed. On application start background worker must be started automatically.

### Features

- [x] Supports multiple tables
- [x] WorkerTypes extensibility
- [x] OutboxItemHandler extensibility (Kafka, HTTP call etc)
- [x] Single or Batched handler
- [x] Strict ordering / competing workers
- [x] Postres implementation is out of the box. You can provide implementation for other databases
- [x] Priority processing supported
- [x] Metrics are collected (OpenTelemetry)
- [x] Maintenance API
- [x] Distributed tracing enabled in w3c format (in Sstv.Outbox.Kafka lib)
- [x] Autopartitioning outbox tables when partitioning enabled

### Metrics

The library collects different metrics that can help to monitor.

Measures duration of worker process one batch. From fetch data from database to fully processed and saved.
It can be helpful to detect performance problems.
```text
# TYPE outbox_worker_process_duration histogram
# HELP outbox_worker_process_duration Measures duration of worker process one batch.
outbox_worker_process_duration_bucket{outbox_name="KafkaEfOutboxItem",le="0"} 0

outbox_worker_process_duration_sum{outbox_name="KafkaEfOutboxItem"} 0
outbox_worker_process_duration_count{outbox_name="KafkaEfOutboxItem"} 0
```

Measures duration of worker sleep between batches.
Worker may sleep lesser than OutboxDelay setting, because of use [System.Threading.PeriodicTimer](https://learn.microsoft.com/ru-ru/dotnet/api/system.threading.periodictimer?view=net-8.0).
So it is important to know how much time worker actually sleeps.
```text
# TYPE outbox_worker_sleep_duration histogram
# HELP outbox_worker_sleep_duration Measures duration of worker sleep between batches.
outbox_worker_sleep_duration_bucket{outbox_name="KafkaEfOutboxItem",le="0"}
outbox_worker_sleep_duration_sum{outbox_name="KafkaEfOutboxItem"}
outbox_worker_sleep_duration_count{outbox_name="KafkaEfOutboxItem"}
```

Measures duration of processing by outbox item handler.
```text
# TYPE outbox_worker_handler_duration histogram
# HELP outbox_worker_handler_duration Measures duration of processing by outbox item handler.
outbox_worker_handler_duration_bucket{batched="False",outbox_name="KafkaEfOutboxItem",le="0"}
outbox_worker_handler_duration_sum{batched="False",outbox_name="KafkaEfOutboxItem"}
outbox_worker_handler_duration_count{batched="False",outbox_name="KafkaEfOutboxItem"}
```

Counts how many outbox items fetched from database.
```text
# TYPE outbox_items_fetched_total counter
# HELP outbox_items_fetched_total Counts how many outbox items fetched.
outbox_items_fetched_total{outbox_name="KafkaEfOutboxItem"}
```

Counts how many outbox items processed.
```text
# TYPE outbox_items_processed_total counter
# HELP outbox_items_processed_total Counts how many outbox items processed.
outbox_items_processed_total{outbox_name="KafkaEfOutboxItem"}
```

Counts how many outbox items retried.
```text
# TYPE outbox_items_retried counter
# HELP outbox_items_retried_total Counts how many outbox items retried.
outbox_items_retried_total{outbox_name="KafkaEfOutboxItem"}
```

Counts how many times full batches have been fetched.
If you set OutboxItemsLimit = 100, this metric shows you, how many times worker fetched from database 100 items.
It may indicate high worker utilization. Consider adding more instances of workers if you use competing workers.
```text
# TYPE outbox_items_full_batches counter
# HELP outbox_items_full_batches_total Counts how many times fetched full batches.
outbox_items_full_batches_total{outbox_name="KafkaEfOutboxItem"}
```
