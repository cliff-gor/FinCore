using FinCore.Modules.Organization.Domain.Errors;
using FinCore.SharedKernel.Domain;
using FinCore.SharedKernel.Results;

namespace FinCore.Modules.Organization.Domain.Institutions;

/// <summary>A Bank or MFI operating on the platform, with its own business date (spec §12).</summary>
public sealed class Institution : AggregateRoot<InstitutionId>
{
    public const int NameMinLength = 2;
    public const int NameMaxLength = 200;
    public const int LicenseNumberMaxLength = 50;

    private Institution(
        InstitutionId id,
        InstitutionCode code,
        string name,
        InstitutionType type,
        string baseCurrency,
        string? licenseNumber,
        DateOnly businessDate)
        : base(id)
    {
        Code = code;
        Name = name;
        Type = type;
        BaseCurrency = baseCurrency;
        LicenseNumber = licenseNumber;
        CurrentBusinessDate = businessDate;
        Status = InstitutionStatus.Active;
    }

    private Institution()
    {
        Code = null!;
        Name = null!;
        BaseCurrency = null!;
    }

    public InstitutionCode Code { get; private set; }

    public string Name { get; private set; }

    public InstitutionType Type { get; private set; }

    /// <summary>ISO-4217 code; existence in the currency reference table is checked by the application layer.</summary>
    public string BaseCurrency { get; private set; }

    public string? LicenseNumber { get; private set; }

    public InstitutionStatus Status { get; private set; }

    public DateOnly CurrentBusinessDate { get; private set; }

    public static Result<Institution> Create(
        string? code,
        string? name,
        InstitutionType type,
        string baseCurrency,
        string? licenseNumber,
        DateOnly initialBusinessDate,
        DateTime utcNow)
    {
        var codeResult = InstitutionCode.Create(code);
        if (codeResult.IsFailure)
        {
            return codeResult.Error;
        }

        var trimmedName = name?.Trim() ?? string.Empty;
        if (trimmedName.Length is < NameMinLength or > NameMaxLength)
        {
            return OrganizationErrors.InvalidInstitutionName;
        }

        if (!Enum.IsDefined(type))
        {
            return OrganizationErrors.InvalidInstitutionType;
        }

        var license = string.IsNullOrWhiteSpace(licenseNumber) ? null : licenseNumber.Trim();
        if (license is { Length: > LicenseNumberMaxLength })
        {
            return OrganizationErrors.InvalidLicenseNumber;
        }

        if (IsWeekend(initialBusinessDate))
        {
            return OrganizationErrors.BusinessDateOnWeekend;
        }

        var institution = new Institution(
            InstitutionId.New(), codeResult.Value, trimmedName, type, baseCurrency, license, initialBusinessDate);

        institution.Raise(new InstitutionCreatedDomainEvent(
            Guid.CreateVersion7(),
            utcNow,
            institution.Id,
            institution.Code.Value,
            institution.Name,
            institution.Type,
            institution.BaseCurrency,
            institution.CurrentBusinessDate));

        return institution;
    }

    /// <summary>Holiday calendars arrive in M1; until then only Saturday and Sunday are non-working days.</summary>
    public static bool IsWeekend(DateOnly date) =>
        date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
}
