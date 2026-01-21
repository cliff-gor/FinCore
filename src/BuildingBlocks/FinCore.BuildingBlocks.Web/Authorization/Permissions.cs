namespace FinCore.BuildingBlocks.Web.Authorization;

/// <summary>
/// Permission names (master spec §13, spec 002). Policies are named after the permission they require. These strings
/// are part of the contract with the front ends (packages/permissions), so rename only together with them.
/// </summary>
public static class Permissions
{
    public const string ClaimType = "permission";

    public static class Platform
    {
        /// <summary>Create, suspend, reactivate and close institutions; PlatformAdmin only.</summary>
        public const string InstitutionsManage = "platform.institutions.manage";
    }

    public static class Organization
    {
        public const string Manage = "org.manage";
        public const string Read = "org.read";
    }

    public static class Identity
    {
        public const string Manage = "identity.manage";
        public const string Approve = "identity.approve";
    }

    public static class Scope
    {
        /// <summary>See every branch of one's institution instead of only the home branch (spec §24).</summary>
        public const string AllBranches = "scope.all-branches";
    }

    public static class Eod
    {
        public const string Run = "eod.run";
        public const string Read = "eod.read";
    }
}
