using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Sstv.Outbox.Npgsql;
using Sstv.Outbox.Npgsql.EntityFrameworkCore;
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

        builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            options.UseNpgsql(datasource);
            options.UseSnakeCaseNamingConvention();
        });

        builder.Services.AddOutboxItemBatch<MyOutboxItem, MyOutboxItemHandler>();
        builder.Services.AddOutboxItem<OneMoreOutboxItem, OneMoreOutboxItemHandler>();
        builder.Services.AddOutboxItem<StrictOutboxItem, StrictOutboxItemHandler>();
        builder.Services.AddOutboxItem<ApplicationDbContext, EfOutboxItem, EfOutboxItemHandler>();

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

        app.MapGet("/MyOutboxItem/fill", async (CancellationToken ct = default) =>
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
        }).WithTags(nameof(MyOutboxItem));

        app.MapGet("/OneMoreOutboxItem/fill", async (CancellationToken ct = default) =>
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
        }).WithTags(nameof(OneMoreOutboxItem));

        app.MapGet("/StrictOutboxItem/fill", async (CancellationToken ct = default) =>
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
        }).WithTags(nameof(StrictOutboxItem));

        app.MapGet("/EfOutboxItem/fill", async (ApplicationDbContext ctx, CancellationToken ct = default) =>
        {
            var now = DateTimeOffset.UtcNow;
            var items = Enumerable.Range(0, 100).Select(x => new EfOutboxItem
            {
                Id = Uuid.NewSequential(),
                CreatedAt = now,
                Status = OutboxItemStatus.Ready,
                Headers = null,
                RetryAfter = null,
                RetryCount = null,
                Data = Encoding.UTF8.GetBytes($"{x} hello bytes!")
            }).ToArray();

            ctx.EfOutboxItems.AddRange(items);

            await ctx.SaveChangesAsync(ct);

            return Results.Ok();
        }).WithTags(nameof(EfOutboxItem));

        app.MapOutboxMaintenanceEndpoints<MyOutboxItem>();
        app.MapOutboxMaintenanceEndpoints<OneMoreOutboxItem>();
        app.MapOutboxMaintenanceEndpoints<StrictOutboxItem>();

        app.UseSwagger();

        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Outbox maintenance Api v1");
        });

        app.MapPrometheusScrapingEndpoint();

        app.Run();
    }
}