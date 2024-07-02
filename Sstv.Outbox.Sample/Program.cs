using Dapper;
using Microsoft.OpenApi.Models;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Sstv.Outbox.Npgsql;
using System.Text;
using UUIDNext;

namespace Sstv.Outbox;

public class Program
{
    public static void Main(string[] args)
    {
        // TODO: подумать над маппингом всех полей
        DefaultTypeMap.MatchNamesWithUnderscores = true;

        var builder = WebApplication.CreateBuilder(args);

        builder.Host.UseDefaultServiceProvider(o =>
        {
            o.ValidateScopes = true;
            o.ValidateOnBuild = true;
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(b => b.AddService(serviceName: "MyService"))
            .WithMetrics(o =>
            {
                o.AddMeter(OutboxMetricCollector.METER_NAME);
                o.AddAspNetCoreInstrumentation();
                o.AddPrometheusExporter();
            });

        var datasource = new NpgsqlDataSourceBuilder(builder.Configuration.GetConnectionString("Default"))
            .BuildMultiHost()
            .WithTargetSession(TargetSessionAttributes.Primary);

        builder.Services.AddSingleton(datasource);

        builder.Services.AddOutboxItemBatch<MyOutboxItem, MyOutboxItemHandler>();
        builder.Services.AddOutboxItem<OneMoreOutboxItem, OneMoreOutboxItemHandler>();
        builder.Services.AddOutboxItem<StrictOutboxItem, StrictOutboxItemHandler>();

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

        app.MapGet("/push", async (CancellationToken ct = default) =>
        {
            await using var cmd = datasource.CreateCommand(
                """
                INSERT INTO my_outbox_items (id, created_at, status, retry_count, retry_after, headers, data)
                SELECT * FROM unnest(@id, @created_at, @status, @retry_count, @retry_after, @headers, @data);
                """
            );

            var now = DateTimeOffset.UtcNow;
            var items = Enumerable.Range(0, 100).Select(x => new MyOutboxItem
            {
                Id = Uuid.NewSequential(),
                CreatedAt = now,
                Status = OutboxItemStatus.Ready,
                Headers = null,
                RetryAfter = null,
                RetryCount = null,
                Data = Encoding.UTF8.GetBytes($"{x} hello bytes!")
            }).ToArray();

            cmd.Parameters.Add(new NpgsqlParameter<Guid[]>("id", items.Select(e => e.Id).ToArray()));
            cmd.Parameters.Add(new NpgsqlParameter<DateTimeOffset[]>("created_at", items.Select(e => e.CreatedAt).ToArray()));
            cmd.Parameters.Add(new NpgsqlParameter<int[]>("status", items.Select(e => (int)e.Status).ToArray()));
            cmd.Parameters.Add(new NpgsqlParameter<byte[]?[]>("headers", items.Select(e => e.Headers).ToArray()));
            cmd.Parameters.Add(new NpgsqlParameter<byte[]?[]>("data", items.Select(e => e.Data).ToArray()));
            cmd.Parameters.Add(new NpgsqlParameter<DateTimeOffset?[]>("retry_after", items.Select(e => e.RetryAfter).ToArray()));
            cmd.Parameters.Add(new NpgsqlParameter<int?[]>("retry_count", items.Select(e => e.RetryCount).ToArray()));

            await cmd.ExecuteNonQueryAsync(ct);

            return Results.Ok();
        });

        app.MapGet("/push2", async (CancellationToken ct = default) =>
        {
            await using var cmd = datasource.CreateCommand(
                """
                INSERT INTO one_more_outbox_items (id, created_at, status, retry_count, retry_after, headers, data)
                SELECT * FROM unnest(@id, @created_at, @status, @retry_count, @retry_after, @headers, @data);
                """
            );

            var now = DateTimeOffset.UtcNow;
            var items = Enumerable.Range(0, 100).Select(x => new MyOutboxItem
            {
                Id = Uuid.NewSequential(),
                CreatedAt = now,
                Status = OutboxItemStatus.Ready,
                Headers = null,
                RetryAfter = null,
                RetryCount = null,
                Data = Encoding.UTF8.GetBytes($"{x} hello bytes!")
            }).ToArray();

            cmd.Parameters.Add(new NpgsqlParameter<Guid[]>("id", items.Select(e => e.Id).ToArray()));
            cmd.Parameters.Add(new NpgsqlParameter<DateTimeOffset[]>("created_at", items.Select(e => e.CreatedAt).ToArray()));
            cmd.Parameters.Add(new NpgsqlParameter<int[]>("status", items.Select(e => (int)e.Status).ToArray()));
            cmd.Parameters.Add(new NpgsqlParameter<byte[]?[]>("headers", items.Select(e => e.Headers).ToArray()));
            cmd.Parameters.Add(new NpgsqlParameter<byte[]?[]>("data", items.Select(e => e.Data).ToArray()));
            cmd.Parameters.Add(new NpgsqlParameter<DateTimeOffset?[]>("retry_after", items.Select(e => e.RetryAfter).ToArray()));
            cmd.Parameters.Add(new NpgsqlParameter<int?[]>("retry_count", items.Select(e => e.RetryCount).ToArray()));

            await cmd.ExecuteNonQueryAsync(ct);

            return Results.Ok();
        });

        app.MapGet("/push3", async (CancellationToken ct = default) =>
        {
            await using var cmd = datasource.CreateCommand(
                """
                INSERT INTO strict_outbox_items (id, created_at, status, retry_count, retry_after, headers, data)
                SELECT * FROM unnest(@id, @created_at, @status, @retry_count, @retry_after, @headers, @data);
                """
            );

            var now = DateTimeOffset.UtcNow;
            var items = Enumerable.Range(0, 100).Select(x => new MyOutboxItem
            {
                Id = Uuid.NewSequential(),
                CreatedAt = now,
                Status = OutboxItemStatus.Ready,
                Headers = null,
                RetryAfter = null,
                RetryCount = null,
                Data = Encoding.UTF8.GetBytes($"{x} hello bytes!")
            }).ToArray();

            cmd.Parameters.Add(new NpgsqlParameter<Guid[]>("id", items.Select(e => e.Id).ToArray()));
            cmd.Parameters.Add(new NpgsqlParameter<DateTimeOffset[]>("created_at", items.Select(e => e.CreatedAt).ToArray()));
            cmd.Parameters.Add(new NpgsqlParameter<int[]>("status", items.Select(e => (int)e.Status).ToArray()));
            cmd.Parameters.Add(new NpgsqlParameter<byte[]?[]>("headers", items.Select(e => e.Headers).ToArray()));
            cmd.Parameters.Add(new NpgsqlParameter<byte[]?[]>("data", items.Select(e => e.Data).ToArray()));
            cmd.Parameters.Add(new NpgsqlParameter<DateTimeOffset?[]>("retry_after", items.Select(e => e.RetryAfter).ToArray()));
            cmd.Parameters.Add(new NpgsqlParameter<int?[]>("retry_count", items.Select(e => e.RetryCount).ToArray()));

            await cmd.ExecuteNonQueryAsync(ct);

            return Results.Ok();
        });

        app.MapOutboxMaintenanceEndpoints<StrictOutboxItem>();
        app.MapOutboxMaintenanceEndpoints<MyOutboxItem>();
        app.MapOutboxMaintenanceEndpoints<OneMoreOutboxItem>();

        app.UseSwagger();

        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Outbox maintenance Api v1");
        });

        app.MapPrometheusScrapingEndpoint();

        app.Run();
    }
}