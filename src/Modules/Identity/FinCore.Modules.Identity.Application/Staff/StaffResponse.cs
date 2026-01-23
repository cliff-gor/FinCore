using FinCore.Modules.Identity.Domain.Staff;

namespace FinCore.Modules.Identity.Application.Staff;

public sealed record ApprovalLimitResponse(string Type, decimal MaxAmount);

public sealed record StaffResponse(
    Guid Id,
    string Email,
    string Name,
    Guid? InstitutionId,
    Guid? HomeBranchId,
    string Status,
    bool SignInLinked,
    IReadOnlyList<string> Roles,
    IReadOnlyList<ApprovalLimitResponse> ApprovalLimits,
    DateTime CreatedAtUtc);

/// <summary>
/// Result of changing an approval limit: lowering applies at once (<see cref="Applied"/>), raising waits for a
/// checker (<see cref="PendingApproval"/>, HTTP 202).
/// </summary>
public sealed record LimitChangeResponse(StaffResponse? Applied, Approvals.ApprovalResponse? PendingApproval);

internal static class StaffMapping
{
    public static StaffResponse ToResponse(StaffUser s) => new(
        s.Id.Value,
        s.Email,
        s.Name,
        s.InstitutionId,
        s.HomeBranchId,
        s.Status.ToString(),
        s.ExternalSubjectId is not null,
        s.Roles.Select(r => r.Role.ToString()).Order(StringComparer.Ordinal).ToList(),
        s.ApprovalLimits.OrderBy(l => l.Type).Select(l => new ApprovalLimitResponse(l.Type.ToString(), l.MaxAmount)).ToList(),
        s.CreatedAtUtc);
}
