using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sstv.Outbox;

public sealed class ApplicationDbContext : DbContext
{
    public DbSet<EfOutboxItem> EfOutboxItems { get; set; } = null!;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
    }
}

public sealed class EfOutboxItemConfiguration : IEntityTypeConfiguration<EfOutboxItem>
{
    public void Configure(EntityTypeBuilder<EfOutboxItem> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.HasIndex(x => new { x.CreatedAt, x.RetryAfter, x.Status });
    }
}

public sealed class EfOutboxItem : IOutboxItem, IHasStatus
{
    /// <summary>
    /// Unique identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Created at timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Status.
    /// </summary>
    public OutboxItemStatus Status { get; set; }

    /// <summary>
    /// How many times retried already.
    /// </summary>
    public int? RetryCount { get; set; }

    /// <summary>
    /// When to retry.
    /// </summary>
    public DateTimeOffset? RetryAfter { get; set; }

    /// <summary>
    /// Headers.
    /// </summary>
    public byte[]? Headers { get; set; }

    /// <summary>
    /// Data.
    /// </summary>
    public byte[]? Data { get; set; }
}

/// <summary>
/// Handler for EfOutboxItem.
/// </summary>
public sealed class EfOutboxItemHandler : IOutboxItemHandler<EfOutboxItem>
{
    /// <summary>
    /// Processes outbox item.
    /// </summary>
    /// <param name="item">Outbox item.</param>
    /// <param name="options">Outbox options.</param>
    /// <param name="ct">Token for cancelling operation.</param>
    public Task<OutboxItemHandleResult> HandleAsync(
        EfOutboxItem item,
        OutboxOptions options,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(item);

        Console.WriteLine("EfOutboxItem Handled {0} retry number: {1}", item.Id.ToString(), item.RetryCount.GetValueOrDefault(0).ToString());

        return Task.FromResult(OutboxItemHandleResult.Ok);
    }
}