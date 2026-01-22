using FinCore.BuildingBlocks.Application.Pagination;
using FinCore.Modules.Organization.Application.Branches;
using FinCore.Modules.Organization.Application.Institutions;

namespace FinCore.Modules.Organization.Application.Abstractions;

/// <summary>Read side (CQRS-light): projections straight to response DTOs, no aggregates.</summary>
public interface IOrganizationQueries
{
    Task<InstitutionResponse?> GetInstitutionAsync(Guid id, CancellationToken cancellationToken);

    /// <param name="onlyInstitutionId">Restricts the list to one institution (institution isolation); null lists all.</param>
    Task<PagedResult<InstitutionResponse>> ListInstitutionsAsync(Guid? onlyInstitutionId, int page, int pageSize, CancellationToken cancellationToken);

    Task<DateOnly?> GetBusinessDateAsync(Guid id, CancellationToken cancellationToken);

    Task<BranchResponse?> GetBranchAsync(Guid institutionId, Guid branchId, CancellationToken cancellationToken);

    /// <param name="onlyBranchId">Branch scope: restricts the list to the caller's home branch; null lists all.</param>
    Task<PagedResult<BranchResponse>> ListBranchesAsync(Guid institutionId, Guid? onlyBranchId, int page, int pageSize, CancellationToken cancellationToken);
}
