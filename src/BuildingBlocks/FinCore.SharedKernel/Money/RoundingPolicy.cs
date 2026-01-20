namespace FinCore.SharedKernel.Money;

/// <summary>How amounts are rounded to currency minor units at posting time (ADR-0004).</summary>
public sealed record RoundingPolicy(MidpointRounding Mode)
{
    /// <summary>Banker's rounding — the platform default.</summary>
    public static readonly RoundingPolicy Default = new(MidpointRounding.ToEven);
}
