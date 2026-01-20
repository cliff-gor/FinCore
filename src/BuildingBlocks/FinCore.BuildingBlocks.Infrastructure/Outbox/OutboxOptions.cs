namespace FinCore.BuildingBlocks.Infrastructure.Outbox;

/// <summary>Outbox processing settings (section <c>Outbox</c>). Defaults follow spec 001.</summary>
public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(2);

    public int BatchSize { get; set; } = 20;

    /// <summary>After this many failed attempts the message is dead-lettered.</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Retry n waits BaseRetryDelay × 2^(n-1): 2 s, 4 s, 8 s, 16 s.</summary>
    public TimeSpan BaseRetryDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Above this many pending messages <c>/health/ready</c> reports Degraded.</summary>
    public int BacklogDegradedThreshold { get; set; } = 1000;
}
