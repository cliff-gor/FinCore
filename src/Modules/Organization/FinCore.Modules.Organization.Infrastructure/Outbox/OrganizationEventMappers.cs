using FinCore.BuildingBlocks.Infrastructure.EventBus;
using FinCore.Modules.Organization.Contracts.IntegrationEvents;
using FinCore.Modules.Organization.Domain.Branches;
using FinCore.Modules.Organization.Domain.Institutions;
using FinCore.SharedKernel.Integration;

namespace FinCore.Modules.Organization.Infrastructure.Outbox;

// Each mapper reuses the domain event id as the integration event id, so the outbox row id is stable and the inbox
// can de-duplicate redeliveries (ADR-0003).

internal sealed class InstitutionStatusChangedMapper : DomainEventMapper<InstitutionStatusChangedDomainEvent>
{
    protected override IIntegrationEvent Map(InstitutionStatusChangedDomainEvent e) =>
        new InstitutionStatusChangedIntegrationEvent(e.EventId, e.OccurredOnUtc, e.InstitutionId.Value, e.PreviousStatus.ToString(), e.Status.ToString());
}

internal sealed class BranchCreatedMapper : DomainEventMapper<BranchCreatedDomainEvent>
{
    protected override IIntegrationEvent Map(BranchCreatedDomainEvent e) =>
        new BranchCreatedIntegrationEvent(e.EventId, e.OccurredOnUtc, e.BranchId.Value, e.InstitutionId.Value, e.Code, e.Name, e.ParentBranchId?.Value);
}

internal sealed class BranchClosedMapper : DomainEventMapper<BranchClosedDomainEvent>
{
    protected override IIntegrationEvent Map(BranchClosedDomainEvent e) =>
        new BranchClosedIntegrationEvent(e.EventId, e.OccurredOnUtc, e.BranchId.Value, e.InstitutionId.Value);
}
