using FinCore.BuildingBlocks.Application.Pagination;
using FinCore.Modules.Organization.Application.Institutions;

namespace FinCore.Modules.Organization.Application.Abstractions;

/// <summary>Read side (CQRS-light): projections straight to response DTOs, no aggregates.</summary>
public interface IOrganizationQueries
{
    Task<InstitutionResponse?> GetInstitutionAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<InstitutionResponse>> ListInstitutionsAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task<DateOnly?> GetBusinessDateAsync(Guid id, CancellationToken cancellationToken);
}
