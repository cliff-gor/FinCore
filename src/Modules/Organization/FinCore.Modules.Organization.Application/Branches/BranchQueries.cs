using FinCore.BuildingBlocks.Application.Messaging;
using FinCore.BuildingBlocks.Application.Pagination;
using FinCore.BuildingBlocks.Application.Security;
using FinCore.Modules.Organization.Application.Abstractions;
using FinCore.Modules.Organization.Domain.Errors;
using FinCore.SharedKernel.Results;
using FluentValidation;

namespace FinCore.Modules.Organization.Application.Branches;

public sealed record GetBranchQuery(Guid InstitutionId, Guid BranchId) : IQuery<BranchResponse>;

public sealed record ListBranchesQuery(Guid InstitutionId, int Page = 1, int PageSize = Paging.DefaultPageSize)
    : IQuery<PagedResult<BranchResponse>>;

internal sealed class ListBranchesQueryValidator : AbstractValidator<ListBranchesQuery>
{
    public ListBranchesQueryValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1).WithMessage("Page must be 1 or greater.");
        RuleFor(q => q.PageSize).InclusiveBetween(1, Paging.MaxPageSize)
            .WithMessage($"Page size must be between 1 and {Paging.MaxPageSize}.");
    }
}

internal sealed class GetBranchQueryHandler(IOrganizationQueries queries, ICurrentUser currentUser)
    : IQueryHandler<GetBranchQuery, BranchResponse>
{
    public async Task<Result<BranchResponse>> Handle(GetBranchQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!currentUser.CanAccessInstitution(query.InstitutionId))
        {
            return OrganizationErrors.InstitutionAccessDenied;
        }

        var branch = await queries.GetBranchAsync(query.InstitutionId, query.BranchId, cancellationToken).ConfigureAwait(false);
        // Branch scope: a user without all-branch scope may only open their own home branch (spec §24).
        var scope = currentUser.BranchFilter();
        return branch is null || (scope is not null && scope != branch.Id)
            ? OrganizationErrors.BranchNotFound(query.BranchId)
            : branch;
    }
}

internal sealed class ListBranchesQueryHandler(IOrganizationQueries queries, ICurrentUser currentUser)
    : IQueryHandler<ListBranchesQuery, PagedResult<BranchResponse>>
{
    public async Task<Result<PagedResult<BranchResponse>>> Handle(ListBranchesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!currentUser.CanAccessInstitution(query.InstitutionId))
        {
            return OrganizationErrors.InstitutionAccessDenied;
        }

        return await queries.ListBranchesAsync(query.InstitutionId, currentUser.BranchFilter(), query.Page, query.PageSize, cancellationToken)
            .ConfigureAwait(false);
    }
}
