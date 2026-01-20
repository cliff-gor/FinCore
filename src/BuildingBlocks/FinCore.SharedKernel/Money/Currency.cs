using FinCore.SharedKernel.Domain;

namespace FinCore.SharedKernel.Money;

/// <summary>ISO-4217 currency. <see cref="MinorUnits"/> is the number of decimal places used when posting.</summary>
public sealed record Currency : IValueObject
{
    public Currency(string code, int minorUnits)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        if (code.Length != 3 || !code.All(char.IsAsciiLetterUpper))
        {
            throw new ArgumentException("Currency code must be three uppercase ISO-4217 letters.", nameof(code));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(minorUnits);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minorUnits, 4);

        Code = code;
        MinorUnits = minorUnits;
    }

    public string Code { get; }

    public int MinorUnits { get; }

    public override string ToString() => Code;
}
