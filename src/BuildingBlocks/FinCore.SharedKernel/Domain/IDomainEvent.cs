namespace FinCore.SharedKernel.Domain;

/// <summary>Something that happened inside an aggregate. Raised in-process, mapped to integration events by infrastructure.</summary>
public interface IDomainEvent
{
    Guid EventId { get; }

    DateTime OccurredOnUtc { get; }
}

/// <summary>Convenience base record for domain events.</summary>
public abstract record DomainEvent(Guid EventId, DateTime OccurredOnUtc) : IDomainEvent;
