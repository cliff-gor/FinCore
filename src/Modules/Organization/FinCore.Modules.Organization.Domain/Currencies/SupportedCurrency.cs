namespace FinCore.Modules.Organization.Domain.Currencies;

/// <summary>Reference data: an ISO-4217 currency the platform accepts (table <c>org.currencies</c>).</summary>
public sealed class SupportedCurrency
{
    /// <param name="minorUnits">Decimal places used when posting amounts (ISO-4217, ADR-0004).</param>
    public SupportedCurrency(string isoCode, string name, int minorUnits, bool isActive)
    {
        IsoCode = isoCode;
        Name = name;
        MinorUnits = minorUnits;
        IsActive = isActive;
    }

    public string IsoCode { get; private set; }

    public string Name { get; private set; }

    public int MinorUnits { get; private set; }

    /// <summary>Inactive currencies stay in the table for history but cannot be chosen for new institutions.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Seeded currencies (spec 001). UGX has no minor units per ISO-4217.</summary>
    public static IReadOnlyList<SupportedCurrency> Seed { get; } =
    [
        new("KES", "Kenyan Shilling", 2, true),
        new("USD", "US Dollar", 2, true),
        new("EUR", "Euro", 2, true),
        new("GBP", "Pound Sterling", 2, true),
        new("UGX", "Uganda Shilling", 0, true),
        new("TZS", "Tanzanian Shilling", 2, true),
    ];
}
