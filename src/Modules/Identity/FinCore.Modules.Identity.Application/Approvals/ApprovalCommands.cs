using FinCore.BuildingBlocks.Application.Audit;
using FinCore.BuildingBlocks.Application.Messaging;
using FinCore.BuildingBlocks.Application.Persistence;
using FinCore.BuildingBlocks.Application.Security;
using FinCore.Modules.Identity.Application.Abstractions;
using FinCore.Modules.Identity.Domain.Approvals;
using FinCore.Modules.Identity.Domain.Errors;
using FinCore.Modules.Identity.Domain.Staff;
using FinCore.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace FinCore.Modules.Identity.Application.Approvals;

public sealed record ApproveRequestCommand(Guid RequestId, string? Comment) : ICommand<ApprovalResponse>, IAuditableCommand
{
    public string AuditEntityId => RequestId.ToString();
}

public sealed record RejectRequestCommand(Guid RequestId, string? Reason) : ICommand<ApprovalResponse>, IAuditableCommand
{
    public string AuditEntityId => RequestId.ToString();
}

public sealed record WithdrawRequestCommand(Guid RequestId) : ICommand<ApprovalResponse>, IAuditableCommand
{
    public string AuditEntityId => RequestId.ToString();
}

/// <summary>Daily job (spec 002): marks pending requests past their 7-day expiry as Expired.</summary>
public sealed record ExpireApprovalRequestsCommand : ICommand<int>;

/// <summary>
/// Loads the request and the deciding staff user, applies a decision and commits. Shared by approve and reject so
/// the eligibility and concurrency handling cannot drift apart.
/// </summary>
internal sealed class ApprovalDecisionService(
    IApprovalRepository approvals,
    IStaffRepository staff,
    IIdentityUnitOfWork unitOfWork,
    IAccessCache accessCache,
    ApprovalPolicy policy,
    IdentityOperationExecutor executor,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task<Result<ApprovalResponse>> DecideAsync(
        Guid requestId, Func<ApprovalRequest, StaffUser, DateTime, Result> decide, CancellationToken cancellationToken)
    {
        var request = await approvals.GetAsync(new ApprovalRequestId(requestId), cancellationToken).ConfigureAwait(false);
        if (request is null)
        {
            return IdentityErrors.ApprovalNotFound(requestId);
        }

        var checker = currentUser.StaffId is { } staffId
            ? await staff.GetAsync(new StaffUserId(staffId), cancellationToken).ConfigureAwait(false)
            : null;
        if (checker is null)
        {
            return IdentityErrors.NotAnEligibleChecker;
        }

        // Expiry and state are reported before eligibility, so a checker learns "expired" or "already decided"
        // rather than a confusing permission error (spec 002 edge cases).
        if (request.Status == ApprovalStatus.Expired)
        {
            return IdentityErrors.ApprovalExpired;
        }

        if (request.Status != ApprovalStatus.Pending)
        {
            return IdentityErrors.ApprovalAlreadyDecided;
        }

        var eligible = policy.CanDecide(request, checker);
        if (eligible.IsFailure)
        {
            return eligible.Error;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var decided = decide(request, checker, now);
        if (decided.IsFailure)
        {
            return decided.Error;
        }

        if (request.Status == ApprovalStatus.Approved)
        {
            // Final approval: carry out Identity's own change in the same transaction as the decision.
            var executed = await executor.ExecuteAsync(request, now, cancellationToken).ConfigureAwait(false);
            if (executed.IsFailure)
            {
                request.Fail(executed.Error.Code, now);
            }
            else
            {
                request.Complete(now);
            }
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ConcurrencyConflictException or UniqueConstraintViolationException)
        {
            // Another checker decided the same request at the same moment; exactly one of them wins. The loser can
            // trip the request's version check or, first, a unique index on the change both tried to apply.
            return IdentityErrors.ApprovalAlreadyDecided;
        }

        if (request.Status == ApprovalStatus.Approved && request.SubjectStaffId is { } subject)
        {
            await accessCache.InvalidateStaffAsync(subject, cancellationToken).ConfigureAwait(false);
        }

        return ApprovalMapping.ToResponse(request);
    }
}

internal sealed class ApproveRequestCommandHandler(ApprovalDecisionService decisions)
    : ICommandHandler<ApproveRequestCommand, ApprovalResponse>
{
    public Task<Result<ApprovalResponse>> Handle(ApproveRequestCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return decisions.DecideAsync(
            command.RequestId,
            (r, checker, now) => r.Approve(checker.Id.Value, command.Comment, now, completesAlone: checker.IsPlatformAdmin),
            cancellationToken);
    }
}

internal sealed class RejectRequestCommandHandler(ApprovalDecisionService decisions)
    : ICommandHandler<RejectRequestCommand, ApprovalResponse>
{
    public Task<Result<ApprovalResponse>> Handle(RejectRequestCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return decisions.DecideAsync(command.RequestId, (r, checker, now) => r.Reject(checker.Id.Value, command.Reason, now), cancellationToken);
    }
}

internal sealed class WithdrawRequestCommandHandler(
    IApprovalRepository approvals,
    IIdentityUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : ICommandHandler<WithdrawRequestCommand, ApprovalResponse>
{
    public async Task<Result<ApprovalResponse>> Handle(WithdrawRequestCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = await approvals.GetAsync(new ApprovalRequestId(command.RequestId), cancellationToken).ConfigureAwait(false);
        if (request is null)
        {
            return IdentityErrors.ApprovalNotFound(command.RequestId);
        }

        var result = request.Withdraw(currentUser.StaffId ?? Guid.Empty, timeProvider.GetUtcNow().UtcDateTime);
        if (result.IsFailure)
        {
            return result.Error;
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ConcurrencyConflictException)
        {
            return IdentityErrors.ApprovalAlreadyDecided;
        }

        return ApprovalMapping.ToResponse(request);
    }
}

internal sealed class ExpireApprovalRequestsCommandHandler(
    IApprovalRepository approvals,
    IIdentityUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<ExpireApprovalRequestsCommandHandler> logger) : ICommandHandler<ExpireApprovalRequestsCommand, int>
{
    public async Task<Result<int>> Handle(ExpireApprovalRequestsCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var due = await approvals.ListDueForExpiryAsync(now, cancellationToken).ConfigureAwait(false);
        var expired = due.Count(r => r.ExpireIfDue(now));

        if (expired > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Expired {Count} approval request(s)", expired);
        }

        return expired;
    }
}

/// <summary>
/// Creates a request for another module's operation (from its <c>ApprovalRequested</c> event, spec 002 cross-module
/// contract). Unknown operation types are refused: an unconfigured operation would have no defined checkers.
/// </summary>
public sealed record SubmitExternalApprovalCommand(
    Guid InstitutionId, string Module, string OperationType, string EntityId, decimal? Amount, string? Currency, string Payload, Guid MakerStaffId)
    : ICommand<ApprovalResponse>;

internal sealed class SubmitExternalApprovalCommandHandler(
    IApprovalRepository approvals,
    IIdentityUnitOfWork unitOfWork,
    ApprovalOperationCatalog catalog,
    TimeProvider timeProvider) : ICommandHandler<SubmitExternalApprovalCommand, ApprovalResponse>
{
    public async Task<Result<ApprovalResponse>> Handle(SubmitExternalApprovalCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var operation = catalog.Find(command.OperationType);
        if (operation is null || command.Module == IdentityOperations.Module)
        {
            return IdentityErrors.UnknownOperation;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var request = ApprovalRequest.Create(
            command.InstitutionId, command.Module, command.OperationType, command.EntityId, subjectStaffId: null,
            command.Amount, command.Currency, command.Payload, command.MakerStaffId,
            ApprovalOperationCatalog.RequiredCheckers(operation, command.Amount), now + catalog.Expiry);

        approvals.Add(request);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ApprovalMapping.ToResponse(request);
    }
}
