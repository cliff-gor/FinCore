using FinCore.Modules.Organization.Contracts.Dtos;

namespace FinCore.Modules.Organization.Contracts;

/// <summary>Read-only API other modules use to query Organization (spec §10). Never returns domain entities.</summary>
public interface IOrganizationModuleApi
{
    Task<InstitutionSummaryDto?> GetInstitutionAsync(Guid institutionId, CancellationToken cancellationToken);

    Task<DateOnly?> GetBusinessDateAsync(Guid institutionId, CancellationToken cancellationToken);
}
