using FinCore.BuildingBlocks.Application.Messaging;
using FinCore.Modules.Organization.Application.Abstractions;
using FinCore.Modules.Organization.Domain.Errors;
using FinCore.SharedKernel.Results;

namespace FinCore.Modules.Organization.Application.Institutions.GetInstitution;

public sealed record GetInstitutionQuery(Guid InstitutionId) : IQuery<InstitutionResponse>;

internal sealed class GetInstitutionQueryHandler(IOrganizationQueries queries) : IQueryHandler<GetInstitutionQuery, InstitutionResponse>
{
    public async Task<Result<InstitutionResponse>> Handle(GetInstitutionQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var institution = await queries.GetInstitutionAsync(query.InstitutionId, cancellationToken).ConfigureAwait(false);
        return institution is null ? OrganizationErrors.InstitutionNotFound(query.InstitutionId) : institution;
    }
}
