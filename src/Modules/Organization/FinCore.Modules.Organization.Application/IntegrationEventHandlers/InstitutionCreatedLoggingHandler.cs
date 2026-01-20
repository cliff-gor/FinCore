using FinCore.BuildingBlocks.Application.EventBus;
using FinCore.Modules.Organization.Contracts.IntegrationEvents;
using Microsoft.Extensions.Logging;

namespace FinCore.Modules.Organization.Application.IntegrationEventHandlers;

/// <summary>
/// M0 walking-skeleton subscriber: proves outbox → bus → inbox end to end (spec 001). Real subscribers
/// (Ledger chart-of-accounts seeding, Identity) replace its purpose in M1.
/// </summary>
internal sealed class InstitutionCreatedLoggingHandler(ILogger<InstitutionCreatedLoggingHandler> logger)
    : IIntegrationEventHandler<InstitutionCreatedIntegrationEvent>
{
    public Task Handle(InstitutionCreatedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        logger.LogInformation(
            "Institution {InstitutionCode} ({InstitutionId}) created with business date {BusinessDate}",
            integrationEvent.Code,
            integrationEvent.InstitutionId,
            integrationEvent.BusinessDate);
        return Task.CompletedTask;
    }
}
