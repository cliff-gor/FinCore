using FinCore.Modules.Identity.Domain.Approvals;
using FinCore.Modules.Identity.Domain.Errors;
using FinCore.Modules.Identity.Domain.Staff;
using FinCore.SharedKernel.Results;

namespace FinCore.Modules.Identity.Application.Approvals;

/// <summary>
/// Who may decide a request (master §13, spec 002). The aggregate covers state and four-eyes; this covers everything
/// that needs to know about the checker: active, permission, institution and authority.
/// </summary>
internal sealed class ApprovalPolicy(ApprovalOperationCatalog catalog)
{
    public Result CanDecide(ApprovalRequest request, StaffUser checker)
    {
        if (!checker.IsActive)
        {
            return IdentityErrors.StaffInactive;
        }

        // Checked here too (not only in the aggregate) so the inbox never offers a request the user cannot decide.
        if (checker.Id.Value == request.MakerStaffId || checker.Id.Value == request.SubjectStaffId)
        {
            return IdentityErrors.FourEyesViolation;
        }

        if (request.Decisions.Any(d => d.CheckerStaffId == checker.Id.Value))
        {
            return IdentityErrors.AlreadyDecidedByChecker;
        }

        // PlatformAdmin is the fallback checker for every institution and has no limits, so a one-admin institution
        // is never deadlocked and its first admin can be given authority (spec 002).
        if (checker.IsPlatformAdmin)
        {
            return Result.Success();
        }

        var operation = catalog.Find(request.OperationType);
        if (operation is null)
        {
            return IdentityErrors.UnknownOperation;
        }

        if (checker.InstitutionId != request.InstitutionId || !checker.Permissions.Contains(operation.Permission))
        {
            return IdentityErrors.NotAnEligibleChecker;
        }

        // Nobody may approve more than they could approve themselves. For a limit raise the amount is the new limit
        // and the relevant type is the one being raised, so nobody grants more authority than they hold.
        var limitType = request.OperationType == IdentityOperations.RaiseApprovalLimit
            ? IdentityPayloads.Deserialize<RaiseLimitPayload>(request.Payload).LimitType
            : operation.LimitType;
        if (limitType is { } type && request.Amount is { } amount && checker.ApprovalLimitFor(type) < amount)
        {
            return IdentityErrors.ApprovalLimitExceeded;
        }

        return Result.Success();
    }
}
