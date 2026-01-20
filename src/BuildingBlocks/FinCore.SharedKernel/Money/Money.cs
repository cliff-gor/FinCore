using FinCore.SharedKernel.Domain;

namespace FinCore.SharedKernel.Money;

/// <summary>An amount in a currency. Always <see cref="decimal"/>; never <c>double</c>.</summary>
public sealed record Money(decimal Amount, Currency Currency) : IValueObject
{
    public static Money Zero(Currency currency) => new(0m, currency);

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return this with { Amount = Amount + other.Amount };
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return this with { Amount = Amount - other.Amount };
    }

    public Money Round(RoundingPolicy policy) =>
        this with { Amount = Math.Round(Amount, Currency.MinorUnits, policy.Mode) };

    public Money Round() => Round(RoundingPolicy.Default);

    public override string ToString() =>
        $"{Amount.ToString("F" + Currency.MinorUnits, System.Globalization.CultureInfo.InvariantCulture)} {Currency.Code}";

    private void EnsureSameCurrency(Money other)
    {
        if (other.Currency != Currency)
        {
            throw new CurrencyMismatchException(Currency, other.Currency);
        }
    }
}
