namespace FinCore.Modules.Identity.Contracts;

/// <summary>Read-only API other modules use to query Identity (spec §10).</summary>
public interface IIdentityModuleApi
{
    /// <summary>
    /// Whether any active staff user has the branch as home branch. Organization asks this before closing a branch
    /// (spec 002): closing it would leave those users without a branch scope.
    /// </summary>
    Task<bool> HasActiveStaffAtBranchAsync(Guid branchId, CancellationToken cancellationToken);
}
