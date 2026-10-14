using FinCore.BuildingBlocks.Application.Messaging;
using FinCore.BuildingBlocks.Application.Pagination;
using FinCore.BuildingBlocks.Application.Security;
using FinCore.Modules.Identity.Application.Abstractions;
using FinCore.Modules.Identity.Domain.Errors;
using FinCore.SharedKernel.Results;
using FluentValidation;

namespace FinCore.Modules.Identity.Application.Staff;

public sealed record GetStaffQuery(Guid StaffId) : IQuery<StaffResponse>;

public sealed record ListStaffQuery(string? Search, Guid? BranchId, int Page = 1, int PageSize = Paging.DefaultPageSize)
    : IQuery<PagedResult<StaffResponse>>;

internal sealed class ListStaffQueryValidator : AbstractValidator<ListStaffQuery>
{
    public ListStaffQueryValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1).WithMessage("Page must be 1 or greater.");
        RuleFor(q => q.PageSize).InclusiveBetween(1, Paging.MaxPageSize)
            .WithMessage($"Page size must be between 1 and {Paging.MaxPageSize}.");
        RuleFor(q => q.Search).MaximumLength(100);
    }
}

internal sealed class GetStaffQueryHandler(ManagedStaffLoader loader, ICurrentUser currentUser) : IQueryHandler<GetStaffQuery, StaffResponse>
{
    public async Task<Result<StaffResponse>> Handle(GetStaffQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var loaded = await loader.LoadAsync(query.StaffId, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return loaded.Error;
        }

        // Branch scope applies to staff records too: a branch-scoped manager sees only their branch's staff.
        var scope = currentUser.BranchFilter();
        return scope is not null && loaded.Value.HomeBranchId != scope
            ? IdentityErrors.StaffNotFound(query.StaffId)
            : StaffMapping.ToResponse(loaded.Value);
    }
}

internal sealed class ListStaffQueryHandler(IIdentityQueries queries, ICurrentUser currentUser)
    : IQueryHandler<ListStaffQuery, PagedResult<StaffResponse>>
{
    public async Task<Result<PagedResult<StaffResponse>>> Handle(ListStaffQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var institution = currentUser.IsPlatformAdmin ? null : currentUser.InstitutionId;
        if (!currentUser.IsPlatformAdmin && institution is null)
        {
            return IdentityErrors.AccessDenied;
        }

        // A branch-scoped user can only ever see their home branch, whatever filter they ask for.
        var branch = currentUser.BranchFilter() ?? query.BranchId;
        return await queries.ListStaffAsync(institution, branch, query.Search, query.Page, query.PageSize, cancellationToken)
            .ConfigureAwait(false);
    }
}
