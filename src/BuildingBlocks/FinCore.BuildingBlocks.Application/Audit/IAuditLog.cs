namespace FinCore.BuildingBlocks.Application.Audit;

/// <summary>A command that names the entity it changed, so audit rows can be searched by entity.</summary>
public interface IAuditableCommand
{
    string? AuditEntityId { get; }
}

/// <summary>Append-only audit trail of successful commands (spec §24).</summary>
public interface IAuditLog
{
    Task RecordAsync(AuditRecord record, CancellationToken cancellationToken);
}

public sealed record AuditRecord(
    string UserId,
    Guid? InstitutionId,
    string Command,
    string? EntityId,
    string? CorrelationId,
    string RequestHash);
