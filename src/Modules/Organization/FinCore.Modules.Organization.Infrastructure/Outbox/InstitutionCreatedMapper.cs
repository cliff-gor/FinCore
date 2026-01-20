using FinCore.BuildingBlocks.Infrastructure.EventBus;
using FinCore.Modules.Organization.Contracts.IntegrationEvents;
using FinCore.Modules.Organization.Domain.Institutions;
using FinCore.SharedKernel.Integration;

namespace FinCore.Modules.Organization.Infrastructure.Outbox;

/// <summary>
/// Translates the internal domain event into the public integration event. Keeping the two separate lets the domain
/// evolve without breaking subscribers in other modules (spec §10).
/// </summary>
internal sealed class InstitutionCreatedMapper : DomainEventMapper<InstitutionCreatedDomainEvent>
{
    protected override IIntegrationEvent Map(InstitutionCreatedDomainEvent domainEvent) =>
        new InstitutionCreatedIntegrationEvent(
            // Reusing the domain event id makes the outbox row id stable, which the inbox relies on for de-duplication.
            domainEvent.EventId,
            domainEvent.OccurredOnUtc,
            domainEvent.InstitutionId.Value,
            domainEvent.Code,
            domainEvent.Name,
            domainEvent.Type.ToString(),
            domainEvent.BaseCurrency,
            domainEvent.BusinessDate);
}
