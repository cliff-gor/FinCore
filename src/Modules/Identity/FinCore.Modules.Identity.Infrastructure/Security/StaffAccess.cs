namespace FinCore.Modules.Identity.Infrastructure.Security;

/// <summary>
/// Everything the claims transformation needs to authorize a staff request, cached per IdP subject. Either a
/// <see cref="DeniedCode"/> (e.g. inactive) or the user's institution, branch and permissions.
/// </summary>
public sealed record StaffAccess(
    Guid StaffId,
    Guid? InstitutionId,
    string? InstitutionStatus,
    Guid? HomeBranchId,
    IReadOnlyList<string> Permissions,
    string? DeniedCode);
