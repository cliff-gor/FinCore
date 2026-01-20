using FinCore.BuildingBlocks.Application.Persistence;
using FinCore.Modules.Organization.Application.Abstractions;
using FinCore.Modules.Organization.Application.Institutions.CreateInstitution;
using FinCore.Modules.Organization.Domain.Errors;
using FinCore.Modules.Organization.Domain.Institutions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FinCore.Modules.Organization.UnitTests;

/// <summary>
/// The create-institution slice with its collaborators substituted: handler outcomes, including the duplicate-code
/// race, and the validator's field-level errors (spec 001 AC-8, AC-9).
/// </summary>
public sealed class CreateInstitutionTests
{
    private static readonly DateOnly Tuesday = new(2026, 10, 13);
    private readonly IInstitutionRepository _repository = Substitute.For<IInstitutionRepository>();
    private readonly IOrganizationUnitOfWork _unitOfWork = Substitute.For<IOrganizationUnitOfWork>();
    private readonly ICurrencyCatalog _currencies = Substitute.For<ICurrencyCatalog>();

    // Only KES and USD count as "active" here, so JPY exercises the unsupported-currency rule.
    public CreateInstitutionTests() =>
        _currencies.IsActiveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<string>() is "KES" or "USD");

    private CreateInstitutionCommandHandler Handler() => new(_repository, _unitOfWork, TimeProvider.System);

    private static CreateInstitutionCommand Command(string code = "fcb2", string currency = "kes", DateOnly? date = null) =>
        new(code, "Test Bank", InstitutionType.Bank, currency, null, date ?? Tuesday);

    [Fact]
    public async Task Creates_and_saves_institution()
    {
        var result = await Handler().Handle(Command(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Code.ShouldBe("FCB2");
        result.Value.BaseCurrency.ShouldBe("KES");
        result.Value.Status.ShouldBe("Active");
        _repository.Received(1).Add(Arg.Is<Institution>(i => i.Code.Value == "FCB2"));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Existing_code_is_a_conflict() // AC-8 sequential
    {
        _repository.CodeExistsAsync("FCB2", Arg.Any<CancellationToken>()).Returns(true);

        var result = await Handler().Handle(Command(), CancellationToken.None);

        result.Error.ShouldBe(OrganizationErrors.InstitutionCodeTaken);
        _repository.DidNotReceive().Add(Arg.Any<Institution>());
    }

    [Fact]
    public async Task Concurrent_duplicate_is_a_conflict() // AC-8 race
    {
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new UniqueConstraintViolationException());

        var result = await Handler().Handle(Command(), CancellationToken.None);

        result.Error.ShouldBe(OrganizationErrors.InstitutionCodeTaken);
    }

    [Fact]
    public async Task Domain_rejection_is_returned()
    {
        var result = await Handler().Handle(Command(code: "X"), CancellationToken.None);
        result.Error.ShouldBe(OrganizationErrors.InvalidInstitutionCode);
    }

    [Fact]
    public async Task Validator_accepts_a_valid_command()
    {
        var validation = await new CreateInstitutionCommandValidator(_currencies).ValidateAsync(Command(), TestContext.Current.CancellationToken);
        validation.IsValid.ShouldBeTrue();
    }

    [Theory] // AC-9
    [InlineData("AB", "kes", 13, "Code")]
    [InlineData("TOOLONG1", "kes", 13, "Code")]
    [InlineData("FCB", "JPY", 13, "BaseCurrency")]
    [InlineData("FCB", "kes", 17, "InitialBusinessDate")]
    public async Task Validator_names_the_failing_field(string code, string currency, int day, string field)
    {
        var validation = await new CreateInstitutionCommandValidator(_currencies)
            .ValidateAsync(Command(code, currency, new DateOnly(2026, 10, day)), TestContext.Current.CancellationToken);

        validation.Errors.ShouldContain(e => e.PropertyName == field);
    }

    [Fact]
    public async Task Validator_rejects_one_character_name_and_missing_date()
    {
        var validation = await new CreateInstitutionCommandValidator(_currencies).ValidateAsync(
            new CreateInstitutionCommand("FCB", "X", InstitutionType.Bank, "KES", null, default), TestContext.Current.CancellationToken);

        validation.Errors.ShouldContain(e => e.PropertyName == "Name");
        validation.Errors.ShouldContain(e => e.PropertyName == "InitialBusinessDate");
    }
}
