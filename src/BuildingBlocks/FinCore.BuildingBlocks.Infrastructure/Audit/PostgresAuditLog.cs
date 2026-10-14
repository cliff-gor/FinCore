using FinCore.BuildingBlocks.Application.Audit;
using FinCore.BuildingBlocks.Infrastructure.Idempotency;

namespace FinCore.BuildingBlocks.Infrastructure.Audit;

/// <summary>
/// Stores audit rows in <c>platform.audit_log</c>. It runs after the command's own transaction committed, so a
/// failure here never undoes a business change; it surfaces as an error in the logs instead.
/// </summary>
internal sealed class PostgresAuditLog(PlatformDbContext dbContext, TimeProvider timeProvider) : IAuditLog
{
    public async Task RecordAsync(AuditRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);

        dbContext.AuditLog.Add(new AuditLogEntry
        {
            Id = Guid.CreateVersion7(),
            OccurredAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            UserId = record.UserId,
            InstitutionId = record.InstitutionId,
            Command = record.Command,
            EntityId = record.EntityId,
            CorrelationId = record.CorrelationId,
            RequestHash = record.RequestHash,
        });
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
