using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Sstv.Outbox.Features.Partitions;

namespace Sstv.Outbox.Npgsql;

/// <summary>
/// Table partitioner.
/// </summary>
public sealed partial class Partitioner<TOutboxItem> : IPartitioner<TOutboxItem>
    where TOutboxItem : class, IOutboxItem
{
    /// <summary>
    /// Options.
    /// </summary>
    private readonly OutboxOptions _options;

    /// <summary>
    /// Logger.
    /// </summary>
    private readonly ILogger<Partitioner<TOutboxItem>> _logger;

    /// <summary>
    /// Creates new instance of <see cref="Partitioner{TOutboxItem}"/>.
    /// </summary>
    /// <param name="options">Options.</param>
    /// <param name="logger">Logger.</param>
    public Partitioner(
        IOptionsMonitor<OutboxOptions> options,
        ILogger<Partitioner<TOutboxItem>> logger
    )
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Get(typeof(TOutboxItem).Name);
        _logger = logger;
    }

    /// <summary>
    /// Precreates partitions for entity.
    /// </summary>
    /// <param name="ct">Token for cancel operation.</param>
    [SuppressMessage("Security", "CA2100:Risk of vulnerability to SQL injection.",
        Justification = "There is no user input.")]
    public async Task CreatePartitionsAsync(CancellationToken ct = default)
    {
        try
        {
            var m = _options.GetDbMapping();
            var connection = await _options
                .GetNpgsqlDataSource()
                .OpenConnectionAsync(ct);

            await using var cmd = connection.CreateCommand();

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

                cmd.CommandText = sql;

                await cmd.ExecuteNonQueryAsync(ct);
            }
        }
        catch (PostgresException e) when (e.Message.Contains("is not partitioned"))
        {
            PartitioningNotConfigured(e, typeof(TOutboxItem).Name);

            throw;
        }
        catch (Exception e)
        {
            CreatePartitionFailed(e, typeof(TOutboxItem).Name);

            throw;
        }
    }

    /// <summary>
    /// Removes old partitions for <typeparamref name="TOutboxItem"/>.
    /// </summary>
    /// <param name="ct">Token for cancel operation.</param>
    [SuppressMessage("Security", "CA2100:Risk of vulnerability to SQL injection.",
        Justification = "There is no user input.")]
    public async Task DeleteOldPartitionsAsync(CancellationToken ct = default)
    {
        try
        {
            var m = _options.GetDbMapping();
            var connection = await _options
                .GetNpgsqlDataSource()
                .OpenConnectionAsync(ct);

            await using var cmd = connection.CreateCommand();

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
                    cmd.CommandText = sql;

                    await cmd.ExecuteNonQueryAsync(ct);

                    sql = $"DROP TABLE {m.SchemaName}.{partition.PartitionTableName};";
                    cmd.CommandText = sql;

                    await cmd.ExecuteNonQueryAsync(ct);
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
                outboxItem: typeof(TOutboxItem).Name
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
        message: "Error occured while precreating partitions for {OutboxItem}"
    )]
    private partial void CreatePartitionFailed(Exception e, string outboxItem);

    [LoggerMessage(
        eventId: 2,
        level: LogLevel.Error,
        message: "Error occured while dropping partitions for {OutboxItem}"
    )]
    private partial void DropPartitionFailed(Exception e, string outboxItem);
}
