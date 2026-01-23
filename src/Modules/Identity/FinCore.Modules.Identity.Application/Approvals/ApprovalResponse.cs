using FinCore.Modules.Identity.Domain.Approvals;

namespace FinCore.Modules.Identity.Application.Approvals;

public sealed record ApprovalDecisionResponse(Guid CheckerStaffId, string Decision, string? Comment, DateTime DecidedAtUtc);

public sealed record ApprovalResponse(
    Guid Id,
    Guid? InstitutionId,
    string Module,
    string OperationType,
    string EntityId,
    Guid? SubjectStaffId,
    decimal? Amount,
    string? Currency,
    string Payload,
    Guid MakerStaffId,
    string Status,
    int RequiredCheckers,
    int Approvals,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    string? OutcomeReason,
    IReadOnlyList<ApprovalDecisionResponse> Decisions);

internal static class ApprovalMapping
{
    public static ApprovalResponse ToResponse(ApprovalRequest r) => new(
        r.Id.Value,
        r.InstitutionId,
        r.Module,
        r.OperationType,
        r.EntityId,
        r.SubjectStaffId,
        r.Amount,
        r.Currency,
        r.Payload,
        r.MakerStaffId,
        r.Status.ToString(),
        r.RequiredCheckers,
        r.ApprovalCount,
        r.CreatedAtUtc,
        r.ExpiresAtUtc,
        r.OutcomeReason,
        r.Decisions.Select(d => new ApprovalDecisionResponse(d.CheckerStaffId, d.Kind.ToString(), d.Comment, d.DecidedAtUtc)).ToList());
}
