namespace FinCore.Modules.Identity.Domain.Approvals;

/// <summary>Operation types owned by Identity itself (spec 002).</summary>
public static class IdentityOperations
{
    public const string Module = "Identity";

    /// <summary>Grant a role to a staff user; payload = role name.</summary>
    public const string GrantRole = "Identity.GrantRole";

    /// <summary>Raise a staff user's approval limit; amount = new limit, payload = limit type.</summary>
    public const string RaiseApprovalLimit = "Identity.RaiseApprovalLimit";
}
