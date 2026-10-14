using FinCore.Modules.Organization.Domain.Institutions;
using FinCore.SharedKernel.Domain;

namespace FinCore.Modules.Organization.Domain.Branches;

public sealed record BranchCreatedDomainEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    BranchId BranchId,
    InstitutionId InstitutionId,
    string Code,
    string Name,
    BranchId? ParentBranchId) : DomainEvent(EventId, OccurredOnUtc);

public sealed record BranchClosedDomainEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    BranchId BranchId,
    InstitutionId InstitutionId) : DomainEvent(EventId, OccurredOnUtc);
