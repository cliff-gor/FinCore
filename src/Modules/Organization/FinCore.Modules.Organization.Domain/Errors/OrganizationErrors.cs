using FinCore.Modules.Organization.Domain.Institutions;
using FinCore.SharedKernel.Results;

namespace FinCore.Modules.Organization.Domain.Errors;

public static class OrganizationErrors
{
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

    public static readonly Error InstitutionCodeTaken = Error.Conflict(
        "Organization.InstitutionCodeTaken", "An institution with this code already exists.");

    public static Error InstitutionNotFound(Guid id) => Error.NotFound(
        "Organization.InstitutionNotFound", $"Institution '{id}' was not found.");
}
