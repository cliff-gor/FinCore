using FinCore.SharedKernel.Integration;

namespace FinCore.BuildingBlocks.Application.EventBus;

public interface IIntegrationEventHandler<in TEvent>
    where TEvent : IIntegrationEvent
{
    Task Handle(TEvent integrationEvent, CancellationToken cancellationToken);
}
