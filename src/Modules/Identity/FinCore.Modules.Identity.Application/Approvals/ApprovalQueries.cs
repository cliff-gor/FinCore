using FinCore.BuildingBlocks.Application.Messaging;
using FinCore.BuildingBlocks.Application.Security;
using FinCore.Modules.Identity.Application.Abstractions;
using FinCore.Modules.Identity.Domain.Approvals;
using FinCore.Modules.Identity.Domain.Errors;
using FinCore.Modules.Identity.Domain.Staff;
using FinCore.SharedKernel.Results;

namespace FinCore.Modules.Identity.Application.Approvals;

/// <param name="Mine">True lists requests the caller made; otherwise the caller's inbox of pending requests.</param>
public sealed record ListApprovalsQuery(bool Mine) : IQuery<IReadOnlyList<ApprovalResponse>>;

public sealed record GetApprovalQuery(Guid RequestId) : IQuery<ApprovalResponse>;

internal sealed class ListApprovalsQueryHandler(
    IApprovalRepository approvals,
    IStaffRepository staff,
    IIdentityQueries queries,
    ApprovalPolicy policy,
    ICurrentUser currentUser) : IQueryHandler<ListApprovalsQuery, IReadOnlyList<ApprovalResponse>>
{
    public async Task<Result<IReadOnlyList<ApprovalResponse>>> Handle(ListApprovalsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (currentUser.StaffId is not { } staffId)
        {
            return IdentityErrors.AccessDenied;
        }

        if (query.Mine)
        {
            var mine = await queries.ListByMakerAsync(staffId, cancellationToken).ConfigureAwait(false);
            return mine.Select(ApprovalMapping.ToResponse).ToList();
        }

        var checker = await staff.GetAsync(new StaffUserId(staffId), cancellationToken).ConfigureAwait(false);
        if (checker is null)
        {
            return IdentityErrors.AccessDenied;
        }

        // The inbox only offers what this user may actually decide (spec 002): not their own requests, not changes
        // to their own access, and nothing above their limit. Pending lists are small, so filtering in memory is fine.
        var pending = await approvals.ListPendingAsync(checker.IsPlatformAdmin ? null : checker.InstitutionId, cancellationToken)
            .ConfigureAwait(false);
        return pending.Where(r => policy.CanDecide(r, checker).IsSuccess).Select(ApprovalMapping.ToResponse).ToList();
    }
}

internal sealed class GetApprovalQueryHandler(
    IApprovalRepository approvals,
    ICurrentUser currentUser) : IQueryHandler<GetApprovalQuery, ApprovalResponse>
{
    public async Task<Result<ApprovalResponse>> Handle(GetApprovalQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var request = await approvals.GetAsync(new ApprovalRequestId(query.RequestId), cancellationToken).ConfigureAwait(false);
        // Visible to its maker and to anyone in the institution who may decide approvals; nobody else, and never
        // across institutions (reported as not found).
        var visible = request is not null
            && (request.MakerStaffId == currentUser.StaffId
                || (request.InstitutionId is { } institution && currentUser.CanAccessInstitution(institution)
                    && currentUser.HasPermission("identity.approve"))
                || currentUser.IsPlatformAdmin);

        return visible ? ApprovalMapping.ToResponse(request!) : IdentityErrors.ApprovalNotFound(query.RequestId);
    }
}
