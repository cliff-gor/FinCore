namespace FinCore.BuildingBlocks.Infrastructure.Audit;

/// <summary>
/// One successful command (spec §24, spec 002). Deliberately stores no request payload: the hash proves what was sent
/// without keeping personal data in an append-only table.
/// </summary>
public sealed class AuditLogEntry
{
    public Guid Id { get; init; }

    public DateTime OccurredAtUtc { get; init; }

    public required string UserId { get; init; }

    public Guid? InstitutionId { get; init; }

    public required string Command { get; init; }

    public string? EntityId { get; init; }

    public string? CorrelationId { get; init; }

    public required string RequestHash { get; init; }
}
