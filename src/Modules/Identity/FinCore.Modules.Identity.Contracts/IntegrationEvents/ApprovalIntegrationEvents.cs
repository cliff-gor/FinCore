using FinCore.SharedKernel.Integration;

namespace FinCore.Modules.Identity.Contracts.IntegrationEvents;

/// <summary>
/// Published by any module whose operation needs maker-checker approval (spec 002 cross-module contract). Identity
/// creates the approval request from it; the requesting module waits for <see cref="ApprovalRequestDecidedIntegrationEvent"/>.
/// </summary>
/// <param name="OperationType">Module-qualified operation name, e.g. <c>Ledger.ManualJournal</c>; must be configured in Identity.</param>
/// <param name="EntityId">The requesting module's id for the thing being approved; echoed back in the decision.</param>
/// <param name="Amount">Monetary amount checked against checkers' approval limits, if the operation has one.</param>
/// <param name="Payload">JSON shown to checkers; the requesting module owns its shape. Never include secrets.</param>
/// <param name="MakerStaffId">FinCore staff id of the person asking; they can never approve their own request.</param>
public sealed record ApprovalRequestedIntegrationEvent(
    Guid Id,
    DateTime OccurredOnUtc,
    Guid InstitutionId,
    string Module,
    string OperationType,
    string EntityId,
    decimal? Amount,
    string? Currency,
    string Payload,
    Guid MakerStaffId) : IIntegrationEvent;

/// <summary>
/// Final outcome of an approval request: <c>Approved</c>, <c>Rejected</c>, <c>Withdrawn</c>, <c>Expired</c> or
/// <c>Failed</c>. The requesting module carries out (or abandons) the operation when it receives this.
/// </summary>
public sealed record ApprovalRequestDecidedIntegrationEvent(
    Guid Id,
    DateTime OccurredOnUtc,
    Guid RequestId,
    Guid? InstitutionId,
    string Module,
    string OperationType,
    string EntityId,
    string Outcome,
    IReadOnlyList<Guid> DecidedBy,
    string? Reason) : IIntegrationEvent;

/// <summary>
/// A staff user's access changed (roles, limits, status). Identity uses it to clear cached permissions; Notifications
/// may tell the user later.
/// </summary>
public sealed record StaffAccessChangedIntegrationEvent(
    Guid Id,
    DateTime OccurredOnUtc,
    Guid StaffId,
    Guid? InstitutionId,
    string Change) : IIntegrationEvent;
