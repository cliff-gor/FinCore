using FinCore.Modules.Organization.Domain.Currencies;
using FinCore.Modules.Organization.Domain.Errors;
using FinCore.Modules.Organization.Domain.Institutions;
using FinCore.SharedKernel.Results;

namespace FinCore.Modules.Organization.UnitTests;

/// <summary>Domain invariants of the Institution aggregate, independent of the database and HTTP (spec 001 AC-7, AC-9).</summary>
public sealed class InstitutionTests
{
    private static readonly DateOnly Tuesday = new(2026, 10, 13);
    private static readonly DateTime Now = new(2026, 10, 13, 8, 0, 0, DateTimeKind.Utc);

    private static Result<Institution> Create(
        string code = "fcb2", string name = "Test Bank", InstitutionType type = InstitutionType.Bank,
        string? license = null, DateOnly? date = null) =>
        Institution.Create(code, name, type, "KES", license, date ?? Tuesday, Now);

    [Fact]
    public void Create_normalizes_code_and_starts_active_with_business_date() // AC-7 (domain part)
    {
        var institution = Create(code: "  fcb2 ", name: "  Test Bank  ").Value;

        institution.Code.Value.ShouldBe("FCB2");
        institution.Name.ShouldBe("Test Bank");
        institution.Status.ShouldBe(InstitutionStatus.Active);
        institution.CurrentBusinessDate.ShouldBe(Tuesday);
        institution.Id.Value.Version.ShouldBe(7);
        institution.LicenseNumber.ShouldBeNull();
    }

    [Fact]
    public void Create_raises_InstitutionCreated_event()
    {
        var institution = Create(license: " LIC-1 ").Value;

        institution.LicenseNumber.ShouldBe("LIC-1");
        var created = institution.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<InstitutionCreatedDomainEvent>();
        created.InstitutionId.ShouldBe(institution.Id);
        created.Code.ShouldBe("FCB2");
        created.BusinessDate.ShouldBe(Tuesday);
        created.OccurredOnUtc.ShouldBe(Now);
    }

    [Theory] // AC-9
    [InlineData("AB")]
    [InlineData("TOOLONG1")]
    [InlineData("FC-B")]
    [InlineData("")]
    public void Invalid_codes_are_rejected(string code)
    {
        Create(code: code).Error.ShouldBe(OrganizationErrors.InvalidInstitutionCode);
    }

    [Fact]
    public void Null_code_is_rejected() =>
        InstitutionCode.Create(null).Error.ShouldBe(OrganizationErrors.InvalidInstitutionCode);

    [Theory]
    [InlineData("X")]
    [InlineData(" ")]
    public void Short_names_are_rejected(string name) =>
        Create(name: name).Error.ShouldBe(OrganizationErrors.InvalidInstitutionName);

    [Fact]
    public void Long_names_and_license_numbers_are_rejected()
    {
        Create(name: new string('a', 201)).Error.ShouldBe(OrganizationErrors.InvalidInstitutionName);
        Create(license: new string('1', 51)).Error.ShouldBe(OrganizationErrors.InvalidLicenseNumber);
    }

    [Fact]
    public void Undefined_type_is_rejected() =>
        Create(type: (InstitutionType)99).Error.ShouldBe(OrganizationErrors.InvalidInstitutionType);

    [Theory]
    [InlineData(2026, 10, 17)] // Saturday
    [InlineData(2026, 10, 18)] // Sunday
    public void Weekend_business_dates_are_rejected(int y, int m, int d) =>
        Create(date: new DateOnly(y, m, d)).Error.ShouldBe(OrganizationErrors.BusinessDateOnWeekend);

    [Fact]
    public void Code_round_trips_from_storage()
    {
        InstitutionCode.FromStorage("FCB").ToString().ShouldBe("FCB");
        InstitutionCode.IsValid("FCMFI").ShouldBeTrue();
    }

    [Fact]
    public void Currency_seed_matches_spec()
    {
        SupportedCurrency.Seed.Select(c => c.IsoCode).ShouldBe(["KES", "USD", "EUR", "GBP", "UGX", "TZS"]);
        SupportedCurrency.Seed.Single(c => c.IsoCode == "UGX").MinorUnits.ShouldBe(0);
        SupportedCurrency.Seed.Where(c => c.IsoCode != "UGX").ShouldAllBe(c => c.MinorUnits == 2 && c.IsActive);
        SupportedCurrency.Seed.ShouldAllBe(c => c.Name.Length > 0);
    }

    [Fact]
    public void Not_found_error_names_the_id()
    {
        var id = Guid.NewGuid();
        var error = OrganizationErrors.InstitutionNotFound(id);
        error.Code.ShouldBe("Organization.InstitutionNotFound");
        error.Type.ShouldBe(ErrorType.NotFound);
        error.Description.ShouldContain(id.ToString());
    }
}
