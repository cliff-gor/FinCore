using FinCore.Modules.Organization.Domain.Errors;
using FinCore.SharedKernel.Domain;
using FinCore.SharedKernel.Results;

namespace FinCore.Modules.Organization.Domain.Institutions;

/// <summary>
/// 3–6 uppercase alphanumerics, globally unique and immutable — it feeds customer and account number
/// formats later (spec §14, §15). Input is trimmed and uppercased.
/// </summary>
public sealed record InstitutionCode : IValueObject
{
    public const int MinLength = 3;
    public const int MaxLength = 6;

    private InstitutionCode(string value) => Value = value;

    public string Value { get; }

    public static string Normalize(string? raw) => (raw ?? string.Empty).Trim().ToUpperInvariant();

    public static bool IsValid(string normalized) =>
        normalized.Length is >= MinLength and <= MaxLength && normalized.All(char.IsAsciiLetterOrDigit);

    public static Result<InstitutionCode> Create(string? raw)
    {
        var normalized = Normalize(raw);
        return IsValid(normalized)
            ? new InstitutionCode(normalized)
            : OrganizationErrors.InvalidInstitutionCode;
    }

    /// <summary>Rehydration from storage, where the value is already known to be valid.</summary>
    public static InstitutionCode FromStorage(string value) => new(value);

    public override string ToString() => Value;
}
