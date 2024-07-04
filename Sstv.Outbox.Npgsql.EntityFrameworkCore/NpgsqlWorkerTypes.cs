namespace Sstv.Outbox.Npgsql.EntityFrameworkCore;

/// <summary>
/// Worker types that can be used.
/// </summary>
public static class EfCoreWorkerTypes
{
    /// <summary>
    /// All workers active, but only one do his job at the same time.
    /// </summary>
    public const string STRICT_ORDERING = "ef_strict_ordering";

    /// <summary>
    /// All workers of this type grabs different N items and process them concurrently.
    /// </summary>
    public const string COMPETING = "ef_competing";

    /// <summary>
    /// All workers of this type grabs different N items and process them concurrently.
    /// Batch passed to client handler.
    /// </summary>
    public const string BATCH_COMPETING = "ef_batch_competing";
}