using FinCore.BuildingBlocks.Infrastructure.Audit;
using FinCore.BuildingBlocks.Infrastructure.Sessions;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FinCore.BuildingBlocks.Infrastructure.Idempotency;

/// <summary>
/// Cross-cutting platform tables that belong to no business module (schema <c>platform</c>): idempotency records,
/// the audit log, BFF sessions and ASP.NET Core Data Protection keys (spec §25, §24, §30.3, §33.5).
/// It is shared by the API and the gateway so both can run as several instances without sticky sessions.
/// </summary>
public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options) : DbContext(options), IDataProtectionKeyContext
{
    public const string Schema = "platform";

    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    public DbSet<AuditLogEntry> AuditLog => Set<AuditLogEntry>();

    public DbSet<BffSession> BffSessions => Set<BffSession>();

    /// <summary>Data Protection key ring; keeping it in Postgres lets cookies and protected tokens survive restarts.</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

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

        modelBuilder.Entity<AuditLogEntry>(b =>
        {
            b.ToTable("audit_log");
            b.HasKey(e => e.Id);
            b.Property(e => e.UserId).HasMaxLength(200);
            b.Property(e => e.Command).HasMaxLength(200);
            b.Property(e => e.EntityId).HasMaxLength(100);
            b.Property(e => e.CorrelationId).HasMaxLength(128);
            b.Property(e => e.RequestHash).HasMaxLength(64);
            // Auditors search by who and by institution over a time window.
            b.HasIndex(e => new { e.InstitutionId, e.OccurredAtUtc });
            b.HasIndex(e => new { e.UserId, e.OccurredAtUtc });
        });

        modelBuilder.Entity<BffSession>(b =>
        {
            b.ToTable("bff_sessions");
            b.HasKey(e => e.Id);
            b.Property(e => e.Id).HasMaxLength(64);
            b.Property(e => e.Subject).HasMaxLength(200);
            b.Property(e => e.Mode).HasMaxLength(20);
            b.HasIndex(e => e.Subject);
            b.HasIndex(e => e.AbsoluteExpiresAtUtc);
        });

        modelBuilder.Entity<DataProtectionKey>(b => b.ToTable("data_protection_keys"));
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
