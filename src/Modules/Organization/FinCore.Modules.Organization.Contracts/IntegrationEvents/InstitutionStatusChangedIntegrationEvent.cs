using FinCore.SharedKernel.Integration;

namespace FinCore.Modules.Organization.Contracts.IntegrationEvents;

/// <summary>
/// An institution was suspended, reactivated or closed (spec 002). Identity keeps a local copy of the status to block
/// writes (Suspended) and sign-in (Closed) without calling Organization on every request.
/// </summary>
public sealed record InstitutionStatusChangedIntegrationEvent(
    Guid Id,
    DateTime OccurredOnUtc,
    Guid InstitutionId,
    string PreviousStatus,
    string Status) : IIntegrationEvent;

/// <summary>A branch was opened. Consumed by Reporting from M8; published now so the history is complete.</summary>
public sealed record BranchCreatedIntegrationEvent(
    Guid Id,
    DateTime OccurredOnUtc,
    Guid BranchId,
    Guid InstitutionId,
    string Code,
    string Name,
    Guid? ParentBranchId) : IIntegrationEvent;

/// <summary>A branch was closed. Consumed by Reporting from M8.</summary>
public sealed record BranchClosedIntegrationEvent(
    Guid Id,
    DateTime OccurredOnUtc,
    Guid BranchId,
    Guid InstitutionId) : IIntegrationEvent;
