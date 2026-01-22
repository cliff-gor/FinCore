using FinCore.BuildingBlocks.Application.Messaging;
using FinCore.BuildingBlocks.Application.Security;
using FinCore.Modules.Organization.Application.Abstractions;
using FinCore.Modules.Organization.Domain.Errors;
using FinCore.SharedKernel.Results;

namespace FinCore.Modules.Organization.Application.Institutions.GetBusinessDate;

public sealed record BusinessDateResponse(Guid InstitutionId, DateOnly BusinessDate);

public sealed record GetBusinessDateQuery(Guid InstitutionId) : IQuery<BusinessDateResponse>;

internal sealed class GetBusinessDateQueryHandler(IOrganizationQueries queries, ICurrentUser currentUser)
    : IQueryHandler<GetBusinessDateQuery, BusinessDateResponse>
{
    public async Task<Result<BusinessDateResponse>> Handle(GetBusinessDateQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!currentUser.CanAccessInstitution(query.InstitutionId))
        {
            return OrganizationErrors.InstitutionAccessDenied;
        }

        var date = await queries.GetBusinessDateAsync(query.InstitutionId, cancellationToken).ConfigureAwait(false);
        return date is null
            ? OrganizationErrors.InstitutionNotFound(query.InstitutionId)
            : new BusinessDateResponse(query.InstitutionId, date.Value);
    }
}
