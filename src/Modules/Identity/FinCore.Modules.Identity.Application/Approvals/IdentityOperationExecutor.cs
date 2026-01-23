using FinCore.Modules.Identity.Application.Abstractions;
using FinCore.Modules.Identity.Domain.Approvals;
using FinCore.Modules.Identity.Domain.Staff;
using FinCore.SharedKernel.Results;

namespace FinCore.Modules.Identity.Application.Approvals;

/// <summary>
/// Carries out Identity's own approved operations. Everything is re-validated against the current state: a request
/// can sit for days, and the user may have been deactivated or already given the role meanwhile (spec 002 → Failed).
/// </summary>
internal sealed class IdentityOperationExecutor(IStaffRepository staff)
{
    /// <returns>Success, or the error whose code becomes the request's failure reason.</returns>
    public async Task<Result> ExecuteAsync(ApprovalRequest request, DateTime utcNow, CancellationToken cancellationToken)
    {
        switch (request.OperationType)
        {
            case IdentityOperations.GrantRole:
            {
                var payload = IdentityPayloads.Deserialize<GrantRolePayload>(request.Payload);
                var user = await staff.GetAsync(new StaffUserId(payload.StaffId), cancellationToken).ConfigureAwait(false);
                return user is null ? Result.Failure(Domain.Errors.IdentityErrors.StaffNotFound(payload.StaffId)) : user.GrantRole(payload.Role, utcNow);
            }

            case IdentityOperations.RaiseApprovalLimit:
            {
                var payload = IdentityPayloads.Deserialize<RaiseLimitPayload>(request.Payload);
                var user = await staff.GetAsync(new StaffUserId(payload.StaffId), cancellationToken).ConfigureAwait(false);
                if (user is null)
                {
                    return Domain.Errors.IdentityErrors.StaffNotFound(payload.StaffId);
                }

                // If someone already set an equal or higher limit, applying this one would lower it: treat as stale.
                return user.ApprovalLimitFor(payload.LimitType) >= payload.NewLimit
                    ? Result.Failure(Error.Conflict("Identity.LimitAlreadyAtOrAbove", "The limit is already at or above the requested value."))
                    : user.SetApprovalLimit(payload.LimitType, payload.NewLimit, utcNow);
            }

            default:
                // Other modules' operations are executed by those modules when they receive the decision event.
                return Result.Success();
        }
    }
}
