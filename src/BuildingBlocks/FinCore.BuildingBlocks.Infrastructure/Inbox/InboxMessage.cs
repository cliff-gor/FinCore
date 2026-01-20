namespace FinCore.BuildingBlocks.Infrastructure.Inbox;

/// <summary>Records that a handler has processed a message, giving effectively-once handling.</summary>
public sealed class InboxMessage
{
    public Guid MessageId { get; init; }

    public required string HandlerName { get; init; }

    public DateTime ProcessedOnUtc { get; init; }
}
