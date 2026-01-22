using FinCore.Modules.Organization.Domain.Errors;
using FinCore.SharedKernel.Domain;
using FinCore.SharedKernel.Results;

namespace FinCore.Modules.Organization.Domain.Branches;

/// <summary>
/// Exactly three digits, unique within an institution and never changed (spec 002). It becomes the first part of
/// every 13-digit account number from M4 (spec §15), which is why it is numeric and fixed-length.
/// </summary>
public sealed record BranchCode : IValueObject
{
    public const int Length = 3;

    private BranchCode(string value) => Value = value;

    public string Value { get; }

    public static bool IsValid(string? raw) =>
        raw is { Length: Length } && raw.All(char.IsAsciiDigit);

    public static Result<BranchCode> Create(string? raw)
    {
        var trimmed = raw?.Trim();
        return IsValid(trimmed) ? new BranchCode(trimmed!) : OrganizationErrors.InvalidBranchCode;
    }

    /// <summary>Rehydration from storage, where the value is already known to be valid.</summary>
    public static BranchCode FromStorage(string value) => new(value);

    public override string ToString() => Value;
}
