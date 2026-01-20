using FinCore.BuildingBlocks.Application.EventBus;
using FinCore.SharedKernel.Domain;
using FinCore.SharedKernel.Integration;

namespace FinCore.BuildingBlocks.Infrastructure.EventBus;

/// <summary>Translates one domain event type into its public integration event. Implemented in module Infrastructure.</summary>
public interface IDomainEventMapper
{
    Type DomainEventType { get; }

    IIntegrationEvent Map(IDomainEvent domainEvent);
}

public abstract class DomainEventMapper<TDomainEvent> : IDomainEventMapper
    where TDomainEvent : IDomainEvent
{
    public Type DomainEventType => typeof(TDomainEvent);

    public IIntegrationEvent Map(IDomainEvent domainEvent) => Map((TDomainEvent)domainEvent);

    protected abstract IIntegrationEvent Map(TDomainEvent domainEvent);
}

public interface IDomainEventMapperRegistry
{
    /// <summary>Returns the integration event for a domain event, or <c>null</c> if it is internal to the module.</summary>
    IIntegrationEvent? Map(IDomainEvent domainEvent);
}

internal sealed class DomainEventMapperRegistry(IEnumerable<IDomainEventMapper> mappers) : IDomainEventMapperRegistry
{
    private readonly Dictionary<Type, IDomainEventMapper> _mappers = mappers.ToDictionary(m => m.DomainEventType);

    public IIntegrationEvent? Map(IDomainEvent domainEvent) =>
        _mappers.TryGetValue(domainEvent.GetType(), out var mapper) ? mapper.Map(domainEvent) : null;
}
