namespace FinCore.SharedKernel.Domain;

/// <summary>Consistency boundary. Collects domain events until the unit of work stores them in the outbox.</summary>
public abstract class AggregateRoot<TId> : Entity<TId>, IAggregateRoot, IAuditable
    where TId : struct
{
    // Events stay in memory until SaveChanges, when the outbox interceptor turns them into outbox rows in the same
    // transaction. Nothing is published if the transaction rolls back.
    private readonly List<IDomainEvent> _domainEvents = [];

    protected AggregateRoot(TId id)
        : base(id)
    {
    }

    protected AggregateRoot()
    {
    }

    public DateTime CreatedAtUtc { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;

    public DateTime? ModifiedAtUtc { get; private set; }

    public string? ModifiedBy { get; private set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    // Explicit interface implementation: only persistence code that knows about IAuditable can set audit fields.
    void IAuditable.SetCreated(DateTime atUtc, string by)
    {
        CreatedAtUtc = atUtc;
        CreatedBy = by;
    }

    void IAuditable.SetModified(DateTime atUtc, string by)
    {
        ModifiedAtUtc = atUtc;
        ModifiedBy = by;
    }

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
}

/// <summary>Non-generic marker so infrastructure can find aggregates regardless of id type.</summary>
public interface IAggregateRoot
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}
