using FinCore.Modules.Organization.Application.Abstractions;
using FinCore.Modules.Organization.Domain.Institutions;
using FluentValidation;

namespace FinCore.Modules.Organization.Application.Institutions.CreateInstitution;

internal sealed class CreateInstitutionCommandValidator : AbstractValidator<CreateInstitutionCommand>
{
    public CreateInstitutionCommandValidator(ICurrencyCatalog currencies)
    {
        RuleFor(c => c.Code)
            .Must(code => InstitutionCode.IsValid(InstitutionCode.Normalize(code)))
            .WithMessage($"Code must be {InstitutionCode.MinLength}–{InstitutionCode.MaxLength} letters or digits.");

        RuleFor(c => c.Name)
            .Must(name => (name?.Trim().Length ?? 0) is >= Institution.NameMinLength and <= Institution.NameMaxLength)
            .WithMessage($"Name must be {Institution.NameMinLength}–{Institution.NameMaxLength} characters.");

        RuleFor(c => c.Type).IsInEnum().WithMessage("Type must be Bank or Mfi.");

        RuleFor(c => c.LicenseNumber)
            .MaximumLength(Institution.LicenseNumberMaxLength)
            .WithMessage($"License number must be at most {Institution.LicenseNumberMaxLength} characters.");

        RuleFor(c => c.InitialBusinessDate)
            .Must(date => date != default)
            .WithMessage("The initial business date is required.")
            .Must(date => !Institution.IsWeekend(date))
            .WithMessage("The business date must be a working day (not Saturday or Sunday).");

        RuleFor(c => c.BaseCurrency)
            .NotEmpty()
            .WithMessage("Base currency is required.")
            .MustAsync((code, ct) => currencies.IsActiveAsync(code.Trim().ToUpperInvariant(), ct))
            .WithMessage("This currency is not supported.");
    }
}
