using Microsoft.EntityFrameworkCore;

namespace FinCore.BuildingBlocks.Infrastructure.Idempotency;

/// <summary>
/// Cross-cutting platform tables that belong to no business module (schema <c>platform</c>):
/// idempotency records now; Data Protection keys later (spec §33.5).
/// </summary>
public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options) : DbContext(options)
{
    public const string Schema = "platform";

    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<IdempotencyRecord>(b =>
        {
            b.ToTable("idempotency_records");
            b.HasKey(r => r.Key);
            b.Property(r => r.Key).HasMaxLength(500);
            b.Property(r => r.RequestHash).HasMaxLength(64);
            b.Property(r => r.ContentType).HasMaxLength(200);
            b.HasIndex(r => r.ExpiresAtUtc);
        });
    }
}

public sealed class IdempotencyRecord
{
    /// <summary>Scoped key: <c>{method} {route} {user}:{Idempotency-Key}</c>.</summary>
    public required string Key { get; init; }

    public required string RequestHash { get; init; }

    public int StatusCode { get; init; }

    public string? ContentType { get; init; }

    public required byte[] Body { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime ExpiresAtUtc { get; init; }
}
