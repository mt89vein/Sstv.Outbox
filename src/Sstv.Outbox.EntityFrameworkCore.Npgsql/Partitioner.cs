using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Sstv.Outbox.Features.Partitions;

namespace Sstv.Outbox.EntityFrameworkCore.Npgsql;

/// <summary>
/// Table partitioner.
/// </summary>
public sealed partial class Partitioner<TDbContext, TOutboxItem> : IPartitioner<TOutboxItem>
    where TDbContext : DbContext
    where TOutboxItem : class, IOutboxItem
{
    /// <summary>
    /// Options.
    /// </summary>
    private readonly OutboxOptions _options;

    /// <summary>
    /// DbContext.
    /// </summary>
    private readonly TDbContext _dbContext;

    /// <summary>
    /// Logger.
    /// </summary>
    private readonly ILogger<Partitioner<TDbContext, TOutboxItem>> _logger;

    /// <summary>
    /// Creates new instance of <see cref="Partitioner{TDbContext,TOutboxItem}"/>.
    /// </summary>
    /// <param name="dbContext">DbContext.</param>
    /// <param name="options">Options.</param>
    /// <param name="logger">Logger.</param>
    public Partitioner(
        TDbContext dbContext,
        IOptionsMonitor<OutboxOptions> options,
        ILogger<Partitioner<TDbContext, TOutboxItem>> logger
    )
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Get(typeof(TOutboxItem).Name);
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Precreates partitions for entity.
    /// </summary>
    /// <param name="ct">Token for cancel operation.</param>
    [SuppressMessage("Security", "EF1002:Risk of vulnerability to SQL injection.",
        Justification = "There is no user input. FromSqlInterpolated incorrectly sets table name")]
    public async Task CreatePartitionsAsync(CancellationToken ct = default)
    {
        try
        {
            var m = _options.GetDbMapping();

            await _dbContext.Database.BeginTransactionAsync(ct);

            foreach (var partition in _options.PartitionSettings.GetPartitions(m.TableName, DateTimeOffset.UtcNow))
            {
                var from = _options.PartitionSettings.UuidV7Generator.ForDate(partition.DateFrom.UtcDateTime);
                var to = _options.PartitionSettings.UuidV7Generator.ForDate(partition.DateTo.UtcDateTime);

                var sql = $"""
                           CREATE TABLE IF NOT EXISTS {m.SchemaName}.{partition.PartitionTableName} PARTITION OF {m.QualifiedTableName} FOR values
                           FROM (overlay('{from}'::text placing '0000-0000-000000000000' from 15)::uuid)
                           TO   (overlay('{to}'::text placing '0000-0000-000000000000' from 15)::uuid)
                           WITH (fillfactor = 90);
                           """;
                await _dbContext.Database.ExecuteSqlRawAsync(sql, ct);
            }

            await _dbContext.Database.CommitTransactionAsync(ct);
        }
        catch (PostgresException e) when (e.Message.Contains("is not partitioned"))
        {
            PartitioningNotConfigured(e, typeof(TOutboxItem).Name);

            throw;
        }
        catch (Exception e)
        {
            CreatePartitionFailed(
                e,
                outboxItem: typeof(TOutboxItem).Name,
                dbContext: typeof(TDbContext).Name
            );

            throw;
        }
    }

    /// <summary>
    /// Removes old partitions for <typeparamref name="TOutboxItem"/>.
    /// </summary>
    /// <param name="ct">Token for cancel operation.</param>
    public async Task DeleteOldPartitionsAsync(CancellationToken ct = default)
    {
        try
        {
            var m = _options.GetDbMapping();

            var partitionsForDelete = _options
                .PartitionSettings
                .GetPartitions(
                    m.TableName,
                    startFrom: DateTimeOffset.UtcNow.AddDays(-_options.PartitionSettings.PrecreatePartitionCount))
                .Reverse()
                .Skip(_options.PartitionSettings.PartitionRetentionCount);

            foreach (var partition in partitionsForDelete)
            {
                try
                {
                    var sql = $"ALTER TABLE {m.QualifiedTableName} DETACH PARTITION {m.SchemaName}.{partition.PartitionTableName} CONCURRENTLY;";
                    await _dbContext.Database.ExecuteSqlRawAsync(sql, ct);

                    sql = $"DROP TABLE {m.SchemaName}.{partition.PartitionTableName};";
                    await _dbContext.Database.ExecuteSqlRawAsync(sql, ct);
                }
                catch (PostgresException e) when (e.Message.Contains("does not exist"))
                {
                    continue;
                }
            }
        }
        catch (PostgresException e) when (e.Message.Contains("is not partitioned"))
        {
            PartitioningNotConfigured(e, typeof(TOutboxItem).Name);

            throw;
        }
        catch (Exception e)
        {
            DropPartitionFailed(
                e,
                outboxItem: typeof(TOutboxItem).Name,
                dbContext: typeof(TDbContext).Name
            );

            throw;
        }
    }

    [LoggerMessage(
        eventId: 0,
        level: LogLevel.Error,
        message: "Partitioning for {OutboxItem} not configured!"
    )]
    private partial void PartitioningNotConfigured(Exception e, string outboxItem);

    [LoggerMessage(
        eventId: 1,
        level: LogLevel.Error,
        message: "Error occured while precreating partitions for {OutboxItem} in {DbContext}"
    )]
    private partial void CreatePartitionFailed(Exception e, string outboxItem, string dbContext);

    [LoggerMessage(
        eventId: 2,
        level: LogLevel.Error,
        message: "Error occured while dropping partitions for {OutboxItem} in {DbContext}"
    )]
    private partial void DropPartitionFailed(Exception e, string outboxItem, string dbContext);
}
