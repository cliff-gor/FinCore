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

    /// <summary>EF Core materialization only; real instances come from <see cref="Create"/>.</summary>
    private Institution()
    {
        Code = null!;
        Name = null!;
        BaseCurrency = null!;
    }

    /// <summary>Immutable after creation: customer and account numbers embed it from M2 onward (spec §14, §15).</summary>
    public InstitutionCode Code { get; private set; }

    public string Name { get; private set; }

    public InstitutionType Type { get; private set; }

    /// <summary>ISO-4217 code; existence in the currency reference table is checked by the application layer.</summary>
    public string BaseCurrency { get; private set; }

    public string? LicenseNumber { get; private set; }

    public InstitutionStatus Status { get; private set; }

    /// <summary>
    /// The institution's accounting date. Only End of Day moves it forward, one working day at a time (spec §12, §26),
    /// so it is deliberately not settable from outside the aggregate.
    /// </summary>
    public DateOnly CurrentBusinessDate { get; private set; }

    /// <summary>
    /// Validates and creates an institution in <see cref="InstitutionStatus.Active"/> status (spec 001).
    /// The application layer runs the same checks first for friendlier field errors; these are the invariants
    /// that hold no matter who calls the aggregate.
    /// </summary>
    /// <param name="utcNow">Wall-clock time for the event timestamp only — never used for financial dates.</param>
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

        // Blank and whitespace-only license numbers mean "not provided"; store null rather than an empty string.
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

        // The event carries everything subscribers need (Ledger seeds a chart of accounts from it in M3),
        // so nobody has to call back into Organization to fetch details (spec §10).
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
