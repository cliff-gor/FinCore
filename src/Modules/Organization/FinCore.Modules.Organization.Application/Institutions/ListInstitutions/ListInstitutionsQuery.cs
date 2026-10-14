using FinCore.BuildingBlocks.Application.Messaging;
using FinCore.BuildingBlocks.Application.Pagination;
using FinCore.BuildingBlocks.Application.Security;
using FinCore.Modules.Organization.Application.Abstractions;
using FinCore.SharedKernel.Results;
using FluentValidation;

namespace FinCore.Modules.Organization.Application.Institutions.ListInstitutions;

/// <summary>Paged list ordered by code (addition to master spec §12, approved in spec 001).</summary>
public sealed record ListInstitutionsQuery(int Page = 1, int PageSize = Paging.DefaultPageSize) : IQuery<PagedResult<InstitutionResponse>>;

/// <summary>Bounds paging so no client can ask for an unbounded page (spec §28: max 100).</summary>
internal sealed class ListInstitutionsQueryValidator : AbstractValidator<ListInstitutionsQuery>
{
    public ListInstitutionsQueryValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1).WithMessage("Page must be 1 or greater.");
        RuleFor(q => q.PageSize).InclusiveBetween(1, Paging.MaxPageSize)
            .WithMessage($"Page size must be between 1 and {Paging.MaxPageSize}.");
    }
}

internal sealed class ListInstitutionsQueryHandler(IOrganizationQueries queries, ICurrentUser currentUser)
    : IQueryHandler<ListInstitutionsQuery, PagedResult<InstitutionResponse>>
{
    public async Task<Result<PagedResult<InstitutionResponse>>> Handle(ListInstitutionsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Institution isolation: staff see only their own institution; PlatformAdmin and system work see all.
        var only = currentUser.IsPlatformAdmin || currentUser.Kind == UserKind.System ? (Guid?)null : currentUser.InstitutionId ?? Guid.Empty;
        return await queries.ListInstitutionsAsync(only, query.Page, query.PageSize, cancellationToken).ConfigureAwait(false);
    }
}
