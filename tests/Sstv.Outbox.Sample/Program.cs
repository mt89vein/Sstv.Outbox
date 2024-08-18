using System.Diagnostics.CodeAnalysis;
using System.Text;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Sstv.Outbox.EntityFrameworkCore.Npgsql;
using Sstv.Outbox.Kafka;
using Sstv.Outbox.Npgsql;
using Sstv.Outbox.Sample.Extensions;
using UUIDNext;

[assembly: ExcludeFromCodeCoverage(Justification = "Sample project")]

namespace Sstv.Outbox.Sample;

/// <summary>
/// Program.
/// </summary>
public class Program
{
    /// <summary>
    /// Entry point.
    /// </summary>
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Host.UseDefaultServiceProvider(o =>
        {
            o.ValidateScopes = true;
            o.ValidateOnBuild = true;
        });

        builder
            .Services
            .AddOpenTelemetry()
            .ConfigureResource(b => b.AddService(serviceName: "MyService"))
            .WithMetrics(o =>
            {
                o.AddMeter(OutboxMetricCollector.MeterName);
                o.AddAspNetCoreInstrumentation();
                o.AddPrometheusExporter();
            });

        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            var datasource = new NpgsqlDataSourceBuilder(connectionString)
                .EnableDynamicJson()
                .BuildMultiHost()
                .WithTargetSession(TargetSessionAttributes.Primary);

            builder.Services.AddSingleton(datasource);
        }

        builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            options.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>());
            options.UseSnakeCaseNamingConvention();
        });

        builder
            .Services
            .AddOutboxItem<MyOutboxItem>()
            .WithBatchHandler<MyOutboxItem, MyOutboxItemHandler>();

        builder
            .Services
            .AddOutboxItem<OneMoreOutboxItem>()
            .WithHandler<OneMoreOutboxItem, OneMoreOutboxItemHandler>();

        builder
            .Services
            .AddOutboxItem<StrictOutboxItem>()
            .WithHandler<StrictOutboxItem, StrictOutboxItemHandler>();

        builder
            .Services
            .AddOutboxItem<PartitionedOutboxItem>()
            .WithHandler<PartitionedOutboxItem, PartitionedOutboxItemHandler>();

        builder
            .Services
            .AddOutboxItem<KafkaNpgsqlOutboxItem>()
            .ProduceToKafka();

        builder
            .Services
            .AddOutboxItem<KafkaNpgsqlOutboxItemWithPriority>()
            .ProduceToKafka();

        builder
            .Services
            .AddOutboxItem<ApplicationDbContext, EfOutboxItem>()
            .WithHandler<EfOutboxItem, EfOutboxItemHandler>();

        builder
            .Services
            .AddOutboxItem<ApplicationDbContext, KafkaEfOutboxItem>()
            .ProduceToKafka();

        builder
            .Services
            .AddOutboxItem<ApplicationDbContext, KafkaEfOutboxItemWithPriority>()
            .ProduceToKafka();

        builder
            .Services
            .AddOutboxItem<ApplicationDbContext, PartitionedEfOutboxItem>()
            .WithHandler<PartitionedEfOutboxItem, PartitionedEfOutboxItemHandler>();

        SqlMapper.AddTypeHandler(new JsonbHeadersHandler());

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Description = "Outbox maintenance Api",
                Title = "Outbox maintenance Api",
                Version = "v1",
            });
        });

        var app = builder.Build();

        using var scope = app.Services.CreateScope();
        using var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ctx.Database.Migrate();

        app.MapPost("/MyOutboxItem/fill", async (NpgsqlDataSource datasource, CancellationToken ct = default) =>
            {
                await using var cmd = datasource.CreateCommand(
                    """
                    INSERT INTO my_outbox_items (id, created_at, status, retry_count, retry_after, headers, data)
                    SELECT * FROM unnest(@id, @created_at, @status, @retry_count, @retry_after, @headers, @data);
                    """
                );

                var now = DateTimeOffset.UtcNow;
                var items = Enumerable
                    .Range(0, 100)
                    .Select(x => new MyOutboxItem
                    {
                        Id = Uuid.NewSequential(),
                        CreatedAt = now,
                        Status = OutboxItemStatus.Ready,
                        Headers = null,
                        RetryAfter = null,
                        RetryCount = null,
                        Data = Encoding.UTF8.GetBytes($"{x} hello bytes!")
                    })
                    .ToArray();

                cmd.Parameters.Add(new NpgsqlParameter<Guid[]>("id", items.Select(e => e.Id).ToArray()));
                cmd.Parameters.Add(new NpgsqlParameter<DateTimeOffset[]>("created_at", items.Select(e => e.CreatedAt).ToArray()));
                cmd.Parameters.Add(new NpgsqlParameter<int[]>("status", items.Select(e => (int)e.Status).ToArray()));
                cmd.Parameters.Add(new NpgsqlParameter<byte[]?[]>("headers", items.Select(e => e.Headers).ToArray()));
                cmd.Parameters.Add(new NpgsqlParameter<byte[]?[]>("data", items.Select(e => e.Data).ToArray()));
                cmd.Parameters.Add(new NpgsqlParameter<DateTimeOffset?[]>("retry_after", items.Select(e => e.RetryAfter).ToArray()));
                cmd.Parameters.Add(new NpgsqlParameter<int?[]>("retry_count", items.Select(e => e.RetryCount).ToArray()));

                await cmd.ExecuteNonQueryAsync(ct);

                return Results.Ok();
            })
            .WithTags(nameof(MyOutboxItem));

        app.MapPost("/OneMoreOutboxItem/fill", async (NpgsqlDataSource datasource, CancellationToken ct = default) =>
            {
                await using var cmd = datasource.CreateCommand(
                    """
                    INSERT INTO one_more_outbox_items (id, created_at, status, retry_count, retry_after, headers, data)
                    SELECT * FROM unnest(@id, @created_at, @status, @retry_count, @retry_after, @headers, @data);
                    """
                );

                var now = DateTimeOffset.UtcNow;
                var items = Enumerable
                    .Range(0, 100)
                    .Select(x => new MyOutboxItem
                    {
                        Id = Uuid.NewSequential(),
                        CreatedAt = now,
                        Status = OutboxItemStatus.Ready,
                        Headers = null,
                        RetryAfter = null,
                        RetryCount = null,
                        Data = Encoding.UTF8.GetBytes($"{x} hello bytes!")
                    })
                    .ToArray();

                cmd.Parameters.Add(new NpgsqlParameter<Guid[]>("id", items.Select(e => e.Id).ToArray()));
                cmd.Parameters.Add(new NpgsqlParameter<DateTimeOffset[]>("created_at", items.Select(e => e.CreatedAt).ToArray()));
                cmd.Parameters.Add(new NpgsqlParameter<int[]>("status", items.Select(e => (int)e.Status).ToArray()));
                cmd.Parameters.Add(new NpgsqlParameter<byte[]?[]>("headers", items.Select(e => e.Headers).ToArray()));
                cmd.Parameters.Add(new NpgsqlParameter<byte[]?[]>("data", items.Select(e => e.Data).ToArray()));
                cmd.Parameters.Add(new NpgsqlParameter<DateTimeOffset?[]>("retry_after", items.Select(e => e.RetryAfter).ToArray()));
                cmd.Parameters.Add(new NpgsqlParameter<int?[]>("retry_count", items.Select(e => e.RetryCount).ToArray()));

                await cmd.ExecuteNonQueryAsync(ct);

                return Results.Ok();
            })
            .WithTags(nameof(OneMoreOutboxItem));

        app.MapPost("/StrictOutboxItem/fill", async (NpgsqlDataSource datasource, CancellationToken ct = default) =>
            {
                await using var cmd = datasource.CreateCommand(
                    """
                    INSERT INTO strict_outbox_items (id, created_at, status, retry_count, retry_after, headers, data)
                    SELECT * FROM unnest(@id, @created_at, @status, @retry_count, @retry_after, @headers, @data);
                    """
                );

                var now = DateTimeOffset.UtcNow;
                var items = Enumerable
                    .Range(0, 100)
                    .Select(x => new MyOutboxItem
                    {
                        Id = Uuid.NewSequential(),
                        CreatedAt = now,
                        Status = OutboxItemStatus.Ready,
                        Headers = null,
                        RetryAfter = null,
                        RetryCount = null,
                        Data = Encoding.UTF8.GetBytes($"{x} hello bytes!")
                    })
                    .ToArray();

                cmd.Parameters.Add(new NpgsqlParameter<Guid[]>("id", items.Select(e => e.Id).ToArray()));
                cmd.Parameters.Add(new NpgsqlParameter<DateTimeOffset[]>("created_at", items.Select(e => e.CreatedAt).ToArray()));
                cmd.Parameters.Add(new NpgsqlParameter<int[]>("status", items.Select(e => (int)e.Status).ToArray()));
                cmd.Parameters.Add(new NpgsqlParameter<byte[]?[]>("headers", items.Select(e => e.Headers).ToArray()));
                cmd.Parameters.Add(new NpgsqlParameter<byte[]?[]>("data", items.Select(e => e.Data).ToArray()));
                cmd.Parameters.Add(new NpgsqlParameter<DateTimeOffset?[]>("retry_after", items.Select(e => e.RetryAfter).ToArray()));
                cmd.Parameters.Add(new NpgsqlParameter<int?[]>("retry_count", items.Select(e => e.RetryCount).ToArray()));

                await cmd.ExecuteNonQueryAsync(ct);

                return Results.Ok();
            })
            .WithTags(nameof(StrictOutboxItem));

        app.MapPost("/PartitionedStrictOutboxItem/fill", async (NpgsqlDataSource datasource, CancellationToken ct = default) =>
            {
                await using var cmd = datasource.CreateCommand(
                    """
                    INSERT INTO partitioned_strict_outbox_items (id, created_at, status, retry_count, retry_after, headers, data)
                    SELECT * FROM unnest(@id, @created_at, @status, @retry_count, @retry_after, @headers, @data);
                    """
                );

                var now = DateTimeOffset.UtcNow;
                var items = Enumerable
                    .Range(0, 100)
                    .Select(x => new MyOutboxItem
                    {
                        Id = Uuid.NewSequential(),
                        CreatedAt = now,
                        Status = OutboxItemStatus.Ready,
                        Headers = null,
                        RetryAfter = null,
                        RetryCount = null,
                        Data = Encoding.UTF8.GetBytes($"{x} hello bytes!")
                    })
                    .ToArray();

                cmd.Parameters.Add(new NpgsqlParameter<Guid[]>("id", items.Select(e => e.Id).ToArray()));
                cmd.Parameters.Add(new NpgsqlParameter<DateTimeOffset[]>("created_at", items.Select(e => e.CreatedAt).ToArray()));
                cmd.Parameters.Add(new NpgsqlParameter<int[]>("status", items.Select(e => (int)e.Status).ToArray()));
                cmd.Parameters.Add(new NpgsqlParameter<byte[]?[]>("headers", items.Select(e => e.Headers).ToArray()));
                cmd.Parameters.Add(new NpgsqlParameter<byte[]?[]>("data", items.Select(e => e.Data).ToArray()));
                cmd.Parameters.Add(new NpgsqlParameter<DateTimeOffset?[]>("retry_after", items.Select(e => e.RetryAfter).ToArray()));
                cmd.Parameters.Add(new NpgsqlParameter<int?[]>("retry_count", items.Select(e => e.RetryCount).ToArray()));

                await cmd.ExecuteNonQueryAsync(ct);

                return Results.Ok();
            })
            .WithTags(nameof(PartitionedOutboxItem));

        app.MapPost("/EfOutboxItem/fill", async (ApplicationDbContext ctx, CancellationToken ct = default) =>
            {
                var now = DateTimeOffset.UtcNow;
                var items = Enumerable
                    .Range(0, 100)
                    .Select(x => new EfOutboxItem
                    {
                        Id = Uuid.NewSequential(),
                        CreatedAt = now,
                        Status = OutboxItemStatus.Ready,
                        Headers = null,
                        RetryAfter = null,
                        RetryCount = null,
                        Data = Encoding.UTF8.GetBytes($"{x} hello bytes!")
                    })
                    .ToArray();

                ctx.EfOutboxItems.AddRange(items);

                await ctx.SaveChangesAsync(ct);

                return Results.Ok();
            })
            .WithTags(nameof(EfOutboxItem));

        app.MapPost("/PartitionedEfOutboxItem/fill", async (ApplicationDbContext ctx, CancellationToken ct = default) =>
            {
                var now = DateTimeOffset.UtcNow;
                var items = Enumerable
                    .Range(0, 100)
                    .Select(x => new PartitionedEfOutboxItem
                    {
                        Id = Uuid.NewSequential(),
                        CreatedAt = now,
                        Status = OutboxItemStatus.Ready,
                        Headers = null,
                        RetryAfter = null,
                        RetryCount = null,
                        Data = Encoding.UTF8.GetBytes($"{x} hello bytes!")
                    })
                    .ToArray();

                ctx.PartitionedEfOutboxItems.AddRange(items);

                await ctx.SaveChangesAsync(ct);

                return Results.Ok();
            })
            .WithTags(nameof(PartitionedEfOutboxItem));

        app.MapPost("/KafkaEfOutboxItem/add", async (
            ApplicationDbContext ctx,
            IKafkaOutboxItemFactory<KafkaEfOutboxItem> factory,
            CancellationToken ct = default
        ) =>
        {
            var id = Uuid.NewSequential();
            var timestamp = DateTimeOffset.UtcNow.AddSeconds(-1);
            var item = factory.Create(id, new NotificationMessage(
                Id: id,
                CreatedAt: timestamp,
                Text: "message!"
            ));
            item.Timestamp = timestamp;

            ctx.KafkaEfOutboxItems.Add(item);
            await ctx.SaveChangesAsync(ct);

            return Results.Ok();
        }).WithTags(nameof(KafkaEfOutboxItem));

        app.MapOutboxMaintenanceEndpoints<MyOutboxItem>();
        app.MapOutboxMaintenanceEndpoints<OneMoreOutboxItem>();
        app.MapOutboxMaintenanceEndpoints<StrictOutboxItem>();
        app.MapOutboxMaintenanceEndpoints<EfOutboxItem>();
        app.MapOutboxMaintenanceEndpoints<KafkaEfOutboxItem>();
        app.MapOutboxMaintenanceEndpoints<KafkaNpgsqlOutboxItem>();
        app.MapOutboxMaintenanceEndpoints<KafkaEfOutboxItemWithPriority>();
        app.MapOutboxMaintenanceEndpoints<KafkaNpgsqlOutboxItemWithPriority>();
        app.MapOutboxMaintenanceEndpoints<PartitionedOutboxItem>();
        app.MapOutboxMaintenanceEndpoints<PartitionedEfOutboxItem>();

        app.UseSwagger();

        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Outbox maintenance Api v1");
        });

        app.MapPrometheusScrapingEndpoint();

        app.Run();
    }
}
