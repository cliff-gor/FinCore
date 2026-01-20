using FinCore.Modules.Organization.Application.Abstractions;
using FinCore.Modules.Organization.Contracts;
using FinCore.Modules.Organization.Contracts.Dtos;
using FinCore.SharedKernel.Time;

namespace FinCore.Modules.Organization.Infrastructure.ContractsImpl;

internal sealed class OrganizationModuleApi(IOrganizationQueries queries) : IOrganizationModuleApi, IBusinessDateProvider
{
    public async Task<InstitutionSummaryDto?> GetInstitutionAsync(Guid institutionId, CancellationToken cancellationToken)
    {
        var i = await queries.GetInstitutionAsync(institutionId, cancellationToken).ConfigureAwait(false);
        return i is null ? null : new InstitutionSummaryDto(i.Id, i.Code, i.Name, i.Type, i.BaseCurrency, i.Status, i.CurrentBusinessDate);
    }

    public Task<DateOnly?> GetBusinessDateAsync(Guid institutionId, CancellationToken cancellationToken) =>
        queries.GetBusinessDateAsync(institutionId, cancellationToken);

    async ValueTask<DateOnly?> IBusinessDateProvider.GetBusinessDateAsync(Guid institutionId, CancellationToken cancellationToken) =>
        await queries.GetBusinessDateAsync(institutionId, cancellationToken).ConfigureAwait(false);
}
