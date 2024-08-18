using UUIDNext.Generator;

namespace Sstv.Outbox.Features.Partitions;

/// <summary>
/// Partition outbox table.
/// </summary>
public sealed record PartitionSettings
{
    /// <summary>
    /// Uuid v7 generator for timestamp.
    /// </summary>
    internal TimestampUuidV7Generator UuidV7Generator { get; } = new();

    /// <summary>
    /// Enabled or not.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// How many days fits in one partition.
    /// </summary>
    public int DaysPerPartition { get; set; } = 1;

    /// <summary>
    /// How many partitions must be created beforehand.
    /// </summary>
    public int PrecreatePartitionCount { get; set; } = 7;

    /// <summary>
    /// How many partitions should not be deleted before current partition.
    /// </summary>
    public int PartitionRetentionCount { get; set; } = 3;

    /// <summary>
    /// How often run checks, whether need to create new partitions or not.
    /// </summary>
    public TimeSpan PrecreatePartitionPeriod { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Calculates partitions.
    /// </summary>
    /// <param name="tableName">Table name.</param>
    /// <param name="startFrom">From which day need to create partitions.</param>
    /// <returns>Partitions.</returns>
    public IEnumerable<Partition> GetPartitions(string tableName, DateTimeOffset startFrom)
    {
        var day = startFrom;
        for (var i = 0; i < PrecreatePartitionCount; i++)
        {
            var dateFrom = GetStartOfDay(day);
            var dateTo = GetEndOfDay(day.AddDays(DaysPerPartition - 1));
            yield return new Partition($"{tableName}_p{dateFrom:yyyyMMdd}", dateFrom, dateTo);
            day = day.AddDays(DaysPerPartition);
        }
    }

    /// <summary>
    /// Returns start of day.
    /// </summary>
    /// <param name="dt">DateTime.</param>
    private static DateTimeOffset GetStartOfDay(DateTimeOffset dt)
    {
        return new DateTimeOffset(dt.Year, dt.Month, dt.Day, 0, 0, 0, 0, TimeSpan.Zero);
    }

    /// <summary>
    /// Returns end of day.
    /// </summary>
    /// <param name="dt">DateTime.</param>
    private static DateTimeOffset GetEndOfDay(DateTimeOffset dt)
    {
        return new DateTimeOffset(dt.Year, dt.Month, dt.Day, 23, 59, 59, 999, TimeSpan.Zero);
    }
};

internal sealed class TimestampUuidV7Generator : UuidV7Generator
{
    public Guid ForDate(DateTime date)
    {
        return New(date);
    }
}

/// <summary>
/// Partition.
/// </summary>
/// <param name="PartitionTableName">PartitionTableName.</param>
/// <param name="DateFrom">Partition start date.</param>
/// <param name="DateTo">Partition end date.</param>
public readonly record struct Partition(
    string PartitionTableName,
    DateTimeOffset DateFrom,
    DateTimeOffset DateTo
);

/// <summary>
/// Partitioner.
/// </summary>
/// <typeparam name="TOutboxItem"></typeparam>
public interface IPartitioner<TOutboxItem>
    where TOutboxItem : class, IOutboxItem
{
    /// <summary>
    /// Precreates partitions for <typeparamref name="TOutboxItem"/>.
    /// </summary>
    /// <param name="ct">Token for cancel operation.</param>
    Task CreatePartitionsAsync(CancellationToken ct = default);

    /// <summary>
    /// Removes old partitions for <typeparamref name="TOutboxItem"/>.
    /// </summary>
    /// <param name="ct">Token for cancel operation.</param>
    Task DeleteOldPartitionsAsync(CancellationToken ct = default);
}


