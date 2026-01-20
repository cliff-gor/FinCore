namespace FinCore.BuildingBlocks.Web.Authorization;

/// <summary>Permission names (spec §13). Policies are named after the permission they require.</summary>
public static class Permissions
{
    public const string ClaimType = "permission";

    public static class Organization
    {
        public const string Manage = "org.manage";
        public const string Read = "org.read";
    }

    public static class Eod
    {
        public const string Run = "eod.run";
        public const string Read = "eod.read";
    }
}
