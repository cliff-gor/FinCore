using FinCore.BuildingBlocks.Infrastructure.EventBus;
using FinCore.Modules.Organization.Contracts.IntegrationEvents;
using FinCore.Modules.Organization.Domain.Institutions;
using FinCore.SharedKernel.Integration;

namespace FinCore.Modules.Organization.Infrastructure.Outbox;

internal sealed class InstitutionCreatedMapper : DomainEventMapper<InstitutionCreatedDomainEvent>
{
    protected override IIntegrationEvent Map(InstitutionCreatedDomainEvent domainEvent) =>
        new InstitutionCreatedIntegrationEvent(
            domainEvent.EventId,
            domainEvent.OccurredOnUtc,
            domainEvent.InstitutionId.Value,
            domainEvent.Code,
            domainEvent.Name,
            domainEvent.Type.ToString(),
            domainEvent.BaseCurrency,
            domainEvent.BusinessDate);
}
