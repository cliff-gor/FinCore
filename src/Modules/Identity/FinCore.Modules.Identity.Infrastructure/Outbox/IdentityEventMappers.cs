using FinCore.BuildingBlocks.Infrastructure.EventBus;
using FinCore.Modules.Identity.Contracts.IntegrationEvents;
using FinCore.Modules.Identity.Domain.Approvals;
using FinCore.Modules.Identity.Domain.Staff;
using FinCore.SharedKernel.Integration;

namespace FinCore.Modules.Identity.Infrastructure.Outbox;

// Domain event ids are reused as integration event ids so redeliveries are de-duplicated by the inbox (ADR-0003).

internal sealed class StaffAccessChangedMapper : DomainEventMapper<StaffAccessChangedDomainEvent>
{
    protected override IIntegrationEvent Map(StaffAccessChangedDomainEvent e) =>
        new StaffAccessChangedIntegrationEvent(e.EventId, e.OccurredOnUtc, e.StaffId.Value, e.InstitutionId, e.Change);
}

internal sealed class ApprovalRequestDecidedMapper : DomainEventMapper<ApprovalRequestDecidedDomainEvent>
{
    protected override IIntegrationEvent Map(ApprovalRequestDecidedDomainEvent e) =>
        new ApprovalRequestDecidedIntegrationEvent(
            e.EventId, e.OccurredOnUtc, e.RequestId.Value, e.InstitutionId, e.Module, e.OperationType, e.EntityId,
            e.Outcome.ToString(), e.DecidedBy, e.Reason);
}
