using FinCore.SharedKernel.Domain;

namespace FinCore.Modules.Organization.Domain.Institutions;

public sealed record InstitutionStatusChangedDomainEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    InstitutionId InstitutionId,
    InstitutionStatus PreviousStatus,
    InstitutionStatus Status) : DomainEvent(EventId, OccurredOnUtc);
