using FinCore.SharedKernel.Domain;

namespace FinCore.SharedKernel.Money;

/// <summary>An amount in a currency. Always <see cref="decimal"/>; never <c>double</c>.</summary>
public sealed record Money(decimal Amount, Currency Currency) : IValueObject
{
    /// <summary>A zero amount; useful as the seed when summing a list of amounts.</summary>
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

    /// <summary>
    /// Rounds to the currency's minor units. Call this at posting time only: interest and fees are computed at full
    /// precision and rounded once, so residues don't accumulate (ADR-0004).
    /// </summary>
    public Money Round(RoundingPolicy policy) =>
        this with { Amount = Math.Round(Amount, Currency.MinorUnits, policy.Mode) };

    /// <summary>Rounds with the platform default, banker's rounding.</summary>
    public Money Round() => Round(RoundingPolicy.Default);

    // Invariant culture keeps logs and test output stable regardless of the server's locale. User-facing
    // formatting belongs to the clients (packages/money).
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
