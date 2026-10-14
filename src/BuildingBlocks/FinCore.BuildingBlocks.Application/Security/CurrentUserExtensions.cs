namespace FinCore.BuildingBlocks.Application.Security;

public static class CurrentUserExtensions
{
    /// <summary>
    /// Institution isolation (spec 002): staff only ever touch their own institution; PlatformAdmin is the single
    /// cross-institution role. Handlers call this before reading or changing institution-owned data. System work
    /// (seeders, event handlers, jobs) has no HTTP user and is trusted: it only runs code the platform itself started.
    /// </summary>
    public static bool CanAccessInstitution(this ICurrentUser user, Guid institutionId)
    {
        ArgumentNullException.ThrowIfNull(user);
        return user.Kind == UserKind.System || user.IsPlatformAdmin || user.InstitutionId == institutionId;
    }

    /// <summary>
    /// Branch scope (spec §24): without <c>scope.all-branches</c> a staff user sees only their home branch.
    /// Returns the branch to filter by, or null when every branch is visible.
    /// </summary>
    public static Guid? BranchFilter(this ICurrentUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return user.Kind == UserKind.System || user.IsPlatformAdmin || user.HasAllBranches ? null : user.HomeBranchId;
    }
}
