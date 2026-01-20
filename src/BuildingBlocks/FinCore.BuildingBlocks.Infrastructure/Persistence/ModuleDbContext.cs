using FinCore.BuildingBlocks.Application.Persistence;
using FinCore.BuildingBlocks.Infrastructure.Inbox;
using FinCore.BuildingBlocks.Infrastructure.Outbox;
using FinCore.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FinCore.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Base DbContext for every module: own schema, outbox + inbox tables, <c>xmin</c> concurrency on aggregates,
/// and unique-violation translation.
/// </summary>
public abstract class ModuleDbContext(DbContextOptions options) : DbContext(options)
{
    public const string MigrationsHistoryTable = "__ef_migrations";

    /// <summary>PostgreSQL schema owned by the module, e.g. <c>org</c>.</summary>
    public abstract string Schema { get; }

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
        {
            throw UniqueConstraintViolationException.For(pg.ConstraintName, ex);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<OutboxMessage>(b =>
        {
            b.ToTable("outbox_messages");
            b.HasKey(m => m.Id);
            b.Property(m => m.Type).HasMaxLength(500);
            b.Property(m => m.Content).HasColumnType("jsonb");
            b.Property(m => m.TraceParent).HasMaxLength(128);
            b.HasIndex(m => new { m.OccurredOnUtc, m.ProcessedOnUtc })
                .HasFilter("processed_on_utc IS NULL AND dead_lettered_on_utc IS NULL");
        });

        modelBuilder.Entity<InboxMessage>(b =>
        {
            b.ToTable("inbox_messages");
            b.HasKey(m => new { m.MessageId, m.HandlerName });
            b.Property(m => m.HandlerName).HasMaxLength(300);
        });

        base.OnModelCreating(modelBuilder);

        // Optimistic concurrency on every aggregate via PostgreSQL's xmin system column (spec §9).
        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(t => typeof(IAggregateRoot).IsAssignableFrom(t.ClrType) && t.BaseType is null))
        {
            modelBuilder.Entity(entityType.ClrType).Property<uint>("RowVersion").IsRowVersion();
        }
    }
}
