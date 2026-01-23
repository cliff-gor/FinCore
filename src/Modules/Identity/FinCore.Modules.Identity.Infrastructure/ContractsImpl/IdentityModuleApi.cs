using FinCore.Modules.Identity.Application.Abstractions;
using FinCore.Modules.Identity.Contracts;

namespace FinCore.Modules.Identity.Infrastructure.ContractsImpl;

/// <summary>In-process implementation of Identity's read-only API for other modules (spec §10).</summary>
internal sealed class IdentityModuleApi(IStaffRepository staff) : IIdentityModuleApi
{
    public Task<bool> HasActiveStaffAtBranchAsync(Guid branchId, CancellationToken cancellationToken) =>
        staff.HasActiveStaffAtBranchAsync(branchId, cancellationToken);
}
