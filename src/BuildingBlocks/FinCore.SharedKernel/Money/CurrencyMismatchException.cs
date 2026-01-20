namespace FinCore.SharedKernel.Money;

/// <summary>Programming error: arithmetic between different currencies is never a business outcome.</summary>
public sealed class CurrencyMismatchException : InvalidOperationException
{
    public CurrencyMismatchException(Currency expected, Currency actual)
        : base($"Currency mismatch: expected {expected.Code} but got {actual.Code}.")
    {
        Expected = expected;
        Actual = actual;
    }

    public CurrencyMismatchException()
    {
    }

    public CurrencyMismatchException(string message)
        : base(message)
    {
    }

    public CurrencyMismatchException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public Currency? Expected { get; }

    public Currency? Actual { get; }
}
