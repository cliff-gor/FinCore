using FinCore.BuildingBlocks.Application.EventBus;
using FinCore.BuildingBlocks.Application.Messaging;
using FinCore.Modules.Identity.Application.Abstractions;
using FinCore.Modules.Identity.Application.Approvals;
using FinCore.Modules.Identity.Contracts.IntegrationEvents;
using FinCore.Modules.Organization.Contracts.IntegrationEvents;
using Microsoft.Extensions.Logging;

namespace FinCore.Modules.Identity.Application.IntegrationEventHandlers;

/// <summary>Starts Identity's copy of a new institution (spec 002).</summary>
internal sealed class InstitutionCreatedHandler(IInstitutionDirectory institutions)
    : IIntegrationEventHandler<InstitutionCreatedIntegrationEvent>
{
    public Task Handle(InstitutionCreatedIntegrationEvent e, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(e);
        return institutions.AddIfMissingAsync(e.InstitutionId, e.Code, e.BaseCurrency, e.OccurredOnUtc, cancellationToken);
    }
}

/// <summary>
/// Keeps the status copy current and clears cached access for the whole institution, so a suspension or closure takes
/// effect for every signed-in staff member on their next request.
/// </summary>
internal sealed class InstitutionStatusChangedHandler(IInstitutionDirectory institutions, IAccessCache accessCache)
    : IIntegrationEventHandler<InstitutionStatusChangedIntegrationEvent>
{
    public async Task Handle(InstitutionStatusChangedIntegrationEvent e, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(e);
        await institutions.ApplyStatusAsync(e.InstitutionId, e.Status, e.OccurredOnUtc, cancellationToken).ConfigureAwait(false);
        await accessCache.InvalidateInstitutionAsync(e.InstitutionId, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Clears cached access for a user whose roles, limits or status changed (also from other API instances later).</summary>
internal sealed class StaffAccessChangedHandler(IAccessCache accessCache)
    : IIntegrationEventHandler<StaffAccessChangedIntegrationEvent>
{
    public Task Handle(StaffAccessChangedIntegrationEvent e, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(e);
        return accessCache.InvalidateStaffAsync(e.StaffId, cancellationToken);
    }
}

/// <summary>Turns another module's <c>ApprovalRequested</c> into an approval request (spec 002 cross-module contract).</summary>
internal sealed class ApprovalRequestedHandler(IDispatcher dispatcher, ILogger<ApprovalRequestedHandler> logger)
    : IIntegrationEventHandler<ApprovalRequestedIntegrationEvent>
{
    public async Task Handle(ApprovalRequestedIntegrationEvent e, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(e);
        var result = await dispatcher.Send(
            new SubmitExternalApprovalCommand(e.InstitutionId, e.Module, e.OperationType, e.EntityId, e.Amount, e.Currency, e.Payload, e.MakerStaffId),
            cancellationToken).ConfigureAwait(false);

        // A misconfigured operation is a deployment error, not a transient one: retrying would never succeed, so log
        // it loudly instead of letting the outbox retry until dead-lettering.
        if (result.IsFailure)
        {
            logger.LogError("Approval request for {OperationType} ({EntityId}) refused: {ErrorCode}", e.OperationType, e.EntityId, result.Error.Code);
        }
    }
}
