namespace FinCore.BuildingBlocks.Application.Security;

/// <summary>Who is calling, as seen by application code. Never read claims directly in handlers — use this.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>Stable identifier for audit columns: the IdP subject, or <c>system</c> for background work.</summary>
    string Id { get; }

    UserKind Kind { get; }

    /// <summary>FinCore staff id once the IdP account is linked (spec 002); null for customers and system work.</summary>
    Guid? StaffId { get; }

    /// <summary>The institution the staff user belongs to; null for PlatformAdmin, customers (M1) and system work.</summary>
    Guid? InstitutionId { get; }

    Guid? HomeBranchId { get; }

    /// <summary>True when the user may see every branch of their institution (<c>scope.all-branches</c>, spec §24).</summary>
    bool HasAllBranches { get; }

    /// <summary>Platform-level administrator, not tied to an institution (spec 002).</summary>
    bool IsPlatformAdmin { get; }

    bool HasPermission(string permission);
}

public enum UserKind
{
    Anonymous = 0,
    System = 1,
    Staff = 2,
    Customer = 3,
}
