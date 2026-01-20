using FinCore.BuildingBlocks.Application.Messaging;
using FinCore.BuildingBlocks.Application.Pagination;
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

internal sealed class ListInstitutionsQueryHandler(IOrganizationQueries queries)
    : IQueryHandler<ListInstitutionsQuery, PagedResult<InstitutionResponse>>
{
    public async Task<Result<PagedResult<InstitutionResponse>>> Handle(ListInstitutionsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return await queries.ListInstitutionsAsync(query.Page, query.PageSize, cancellationToken).ConfigureAwait(false);
    }
}
