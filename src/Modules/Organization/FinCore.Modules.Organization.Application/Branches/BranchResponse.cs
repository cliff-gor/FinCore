namespace FinCore.Modules.Organization.Application.Branches;

public sealed record BranchResponse(
    Guid Id,
    Guid InstitutionId,
    string Code,
    string Name,
    string? Address,
    Guid? ParentBranchId,
    string Status,
    DateTime CreatedAtUtc);

internal static class BranchMapping
{
    public static BranchResponse ToResponse(Domain.Branches.Branch branch) => new(
        branch.Id.Value,
        branch.InstitutionId.Value,
        branch.Code.Value,
        branch.Name,
        branch.Address,
        branch.ParentBranchId?.Value,
        branch.Status.ToString(),
        branch.CreatedAtUtc);
}
