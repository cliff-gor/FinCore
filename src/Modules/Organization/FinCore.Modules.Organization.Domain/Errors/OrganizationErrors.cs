using FinCore.Modules.Organization.Domain.Institutions;
using FinCore.SharedKernel.Results;

namespace FinCore.Modules.Organization.Domain.Errors;

/// <summary>
/// Every failure the Organization module can report. Codes are part of the public API contract: front ends translate
/// them (packages/i18n), so rename a code only together with a breaking-change note.
/// </summary>
public static class OrganizationErrors
{
    // Field-level failures are ValidationErrors so they surface in ProblemDetails `errors` under the request field name.

    public static readonly Error InvalidInstitutionCode = ValidationError.For(
        "code", $"Code must be {InstitutionCode.MinLength}–{InstitutionCode.MaxLength} letters or digits.");

    public static readonly Error InvalidInstitutionName = ValidationError.For(
        "name", $"Name must be {Institution.NameMinLength}–{Institution.NameMaxLength} characters.");

    public static readonly Error InvalidInstitutionType = ValidationError.For(
        "type", "Type must be Bank or Mfi.");

    public static readonly Error InvalidLicenseNumber = ValidationError.For(
        "licenseNumber", $"License number must be at most {Institution.LicenseNumberMaxLength} characters.");

    public static readonly Error BusinessDateOnWeekend = ValidationError.For(
        "initialBusinessDate", "The business date must be a working day (not Saturday or Sunday).");

    public static readonly Error CurrencyNotSupported = ValidationError.For(
        "baseCurrency", "This currency is not supported.");

    // Business-rule failures carry a stable, module-prefixed code (spec §28).
    public static readonly Error InstitutionCodeTaken = Error.Conflict(
        "Organization.InstitutionCodeTaken", "An institution with this code already exists.");

    public static readonly Error InvalidBranchCode = ValidationError.For(
        "code", $"Branch code must be exactly {Branches.BranchCode.Length} digits.");

    public static readonly Error InvalidBranchName = ValidationError.For(
        "name", $"Name must be {Branches.Branch.NameMinLength}–{Branches.Branch.NameMaxLength} characters.");

    public static readonly Error InvalidBranchAddress = ValidationError.For(
        "address", $"Address must be at most {Branches.Branch.AddressMaxLength} characters.");

    public static readonly Error InvalidParentBranch = ValidationError.For(
        "parentBranchId", "The parent must be an active top-level branch of the same institution.");

    public static readonly Error InvalidWeekendDays = ValidationError.For(
        "weekendDays", "Weekend days must be valid days and leave at least one working day.");

    public static readonly Error PastHolidaysAreImmutable = ValidationError.For(
        "holidays", "Holidays before the current business date cannot be changed.");

    public static readonly Error InvalidHolidays = ValidationError.For(
        "holidays", "Each holiday needs a name (max 100 characters) and dates must be unique.");

    public static readonly Error BranchCodeTaken = Error.Conflict(
        "Organization.BranchCodeTaken", "A branch with this code already exists in the institution.");

    public static readonly Error BranchClosed = Error.Conflict(
        "Organization.BranchClosed", "The branch is closed.");

    public static readonly Error BranchHasActiveStaff = Error.Conflict(
        "Organization.BranchHasActiveStaff", "Active staff still have this branch as their home branch.");

    public static readonly Error BranchHasActiveChildren = Error.Conflict(
        "Organization.BranchHasActiveChildren", "Close the branches that report to this branch first.");

    public static readonly Error InstitutionHasActiveBranches = Error.Conflict(
        "Organization.InstitutionHasActiveBranches", "Close all branches before closing the institution.");

    public static readonly Error InstitutionAccessDenied = Error.Forbidden(
        "Organization.InstitutionAccessDenied", "You can only work with your own institution.");

    public static Error InvalidStatusTransition(InstitutionStatus from, InstitutionStatus to) => Error.Conflict(
        "Organization.InvalidStatusTransition", $"An institution cannot move from {from} to {to}.");

    public static Error BranchNotFound(Guid id) => Error.NotFound(
        "Organization.BranchNotFound", $"Branch '{id}' was not found.");

    public static Error InstitutionNotFound(Guid id) => Error.NotFound(
        "Organization.InstitutionNotFound", $"Institution '{id}' was not found.");
}
