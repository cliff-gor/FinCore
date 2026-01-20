using FinCore.SharedKernel.Money;

namespace SharedKernelTests;

/// <summary>Money arithmetic and rounding rules from ADR-0004 (spec 001 AC-5).</summary>
public sealed class MoneyTests
{
    private static readonly Currency Kes = new("KES", 2);
    private static readonly Currency Usd = new("USD", 2);
    private static readonly Currency Ugx = new("UGX", 0);

    [Fact]
    public void Add_and_subtract_same_currency()
    {
        var a = new Money(100.25m, Kes);
        var b = new Money(0.75m, Kes);

        a.Add(b).ShouldBe(new Money(101.00m, Kes));
        a.Subtract(b).ShouldBe(new Money(99.50m, Kes));
    }

    [Fact]
    public void Adding_different_currencies_throws_CurrencyMismatchException() // AC-5
    {
        var ex = Should.Throw<CurrencyMismatchException>(() => new Money(1m, Kes).Add(new Money(1m, Usd)));
        ex.Expected.ShouldBe(Kes);
        ex.Actual.ShouldBe(Usd);
        Should.Throw<CurrencyMismatchException>(() => new Money(1m, Kes).Subtract(new Money(1m, Usd)));
    }

    [Theory]
    [InlineData("10.005", "10.00")] // AC-5: banker's rounding (midpoint to even)
    [InlineData("10.015", "10.02")]
    [InlineData("10.0051", "10.01")]
    [InlineData("-10.005", "-10.00")]
    public void Round_uses_bankers_rounding_by_default(string amount, string expected)
    {
        new Money(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), Kes).Round()
            .Amount.ShouldBe(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Round_respects_currency_minor_units_and_policy()
    {
        new Money(1234.5m, Ugx).Round().Amount.ShouldBe(1234m);
        new Money(10.005m, Kes).Round(new RoundingPolicy(MidpointRounding.AwayFromZero)).Amount.ShouldBe(10.01m);
    }

    [Fact]
    public void Zero_and_ToString()
    {
        Money.Zero(Kes).Amount.ShouldBe(0m);
        new Money(5m, Kes).ToString().ShouldBe("5.00 KES");
        new Money(5m, Ugx).ToString().ShouldBe("5 UGX");
    }

    [Theory]
    [InlineData("kes", 2)]
    [InlineData("KE", 2)]
    [InlineData("KES1", 2)]
    [InlineData("KES", -1)]
    [InlineData("KES", 5)]
    public void Currency_rejects_invalid_codes_and_minor_units(string code, int minorUnits)
    {
        Should.Throw<ArgumentException>(() => new Currency(code, minorUnits));
    }

    [Fact]
    public void Currency_ToString_is_iso_code()
    {
        Kes.ToString().ShouldBe("KES");
        Kes.MinorUnits.ShouldBe(2);
    }

    [Fact]
    public void Exception_standard_constructors()
    {
        new CurrencyMismatchException().Expected.ShouldBeNull();
        new CurrencyMismatchException("m").Message.ShouldBe("m");
        new CurrencyMismatchException("m", new InvalidOperationException()).InnerException.ShouldNotBeNull();
    }
}
