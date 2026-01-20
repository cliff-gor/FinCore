namespace FinCore.BuildingBlocks.Infrastructure.Outbox;

/// <summary>An integration event stored in the same transaction as the state change that produced it (ADR-0003).</summary>
public sealed class OutboxMessage
{
    public Guid Id { get; init; }

    /// <summary>CLR type used to deserialize <see cref="Content"/> (<c>Namespace.Type, Assembly</c>).</summary>
    public required string Type { get; init; }

    public required string Content { get; init; }

    public DateTime OccurredOnUtc { get; init; }

    /// <summary>W3C trace context of the request that produced the event, so traces span the async hop.</summary>
    public string? TraceParent { get; init; }

    public DateTime? ProcessedOnUtc { get; set; }

    public int Attempts { get; set; }

    public DateTime? NextAttemptOnUtc { get; set; }

    public string? Error { get; set; }

    public DateTime? DeadLetteredOnUtc { get; set; }
}
