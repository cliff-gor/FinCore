using FinCore.SharedKernel.Results;

namespace FinCore.Modules.Identity.Domain.Errors;

/// <summary>
/// Every failure the Identity module reports. Codes are part of the API contract (front ends translate them), so they
/// only change with a breaking-change note.
/// </summary>
public static class IdentityErrors
{
    // Field-level validation.
    public static readonly Error InvalidEmail = ValidationError.For("email", "A valid email address is required.");

    public static readonly Error InvalidName = ValidationError.For("name", "Name must be 2–100 characters.");

    public static readonly Error InvalidApprovalLimit = ValidationError.For("maxAmount", "The limit must be zero or more.");

    public static readonly Error RejectionReasonRequired = ValidationError.For("reason", "A reason is required to reject a request.");

    // Sign-in (spec 002 edge cases).
    public static readonly Error StaffNotRegistered = Error.Forbidden(
        "Identity.StaffNotRegistered", "This account is not registered as FinCore staff. Contact your administrator.");

    public static readonly Error EmailNotVerified = Error.Forbidden(
        "Identity.EmailNotVerified", "Verify your email address with the identity provider before signing in.");

    public static readonly Error StaffInactive = Error.Forbidden(
        "Identity.StaffInactive", "This staff account or its institution is no longer active.");

    public static readonly Error StaffAlreadyLinked = Error.Conflict(
        "Identity.StaffAlreadyLinked", "This staff user is already linked to another sign-in account.");

    // Staff management.
    public static readonly Error StaffEmailTaken = Error.Conflict(
        "Identity.StaffEmailTaken", "A staff user with this email already exists.");

    public static readonly Error RoleAlreadyGranted = Error.Conflict(
        "Identity.RoleAlreadyGranted", "The user already holds this role.");

    public static readonly Error RoleNotGranted = Error.Conflict(
        "Identity.RoleNotGranted", "The user does not hold this role.");

    public static readonly Error RoleNotAssignable = Error.BusinessRule(
        "Identity.RoleNotAssignable", "This role cannot be granted to this user.");

    public static readonly Error StaffNotDeactivated = Error.Conflict(
        "Identity.StaffNotDeactivated", "Only a deactivated user can be reactivated.");

    public static readonly Error BootstrapNotAllowed = Error.Conflict(
        "Identity.BootstrapNotAllowed", "The institution already has an active SystemAdmin; use a normal role grant.");

    public static readonly Error InvalidHomeBranch = ValidationError.For(
        "homeBranchId", "The home branch must be an active branch of the institution.");

    public static readonly Error AccessDenied = Error.Forbidden(
        "Identity.AccessDenied", "You can only manage staff of your own institution.");

    // Maker-checker.
    public static readonly Error FourEyesViolation = Error.Forbidden(
        "Identity.FourEyesViolation", "You cannot decide a request you made or one that changes your own access.");

    public static readonly Error AlreadyDecidedByChecker = Error.Conflict(
        "Identity.AlreadyDecidedByChecker", "You have already decided this request.");

    public static readonly Error ApprovalAlreadyDecided = Error.Conflict(
        "Identity.ApprovalAlreadyDecided", "This request has already been decided.");

    public static readonly Error ApprovalExpired = Error.Conflict(
        "Identity.ApprovalExpired", "This request has expired.");

    public static readonly Error ApprovalLimitExceeded = Error.Forbidden(
        "Identity.ApprovalLimitExceeded", "The amount is above your approval limit.");

    public static readonly Error NotAnEligibleChecker = Error.Forbidden(
        "Identity.NotAnEligibleChecker", "You are not allowed to decide this request.");

    public static readonly Error OnlyMakerCanWithdraw = Error.Forbidden(
        "Identity.OnlyMakerCanWithdraw", "Only the person who made the request can withdraw it.");

    public static readonly Error UnknownOperation = Error.BusinessRule(
        "Identity.UnknownOperation", "This operation type is not configured for approval.");

    public static Error StaffNotFound(Guid id) => Error.NotFound("Identity.StaffNotFound", $"Staff user '{id}' was not found.");

    public static Error ApprovalNotFound(Guid id) => Error.NotFound("Identity.ApprovalNotFound", $"Approval request '{id}' was not found.");
}
