using FinCore.SharedKernel.Domain;

namespace FinCore.Modules.Organization.Domain.Institutions;

public sealed record InstitutionCreatedDomainEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    InstitutionId InstitutionId,
    string Code,
    string Name,
    InstitutionType Type,
    string BaseCurrency,
    DateOnly BusinessDate) : DomainEvent(EventId, OccurredOnUtc);
