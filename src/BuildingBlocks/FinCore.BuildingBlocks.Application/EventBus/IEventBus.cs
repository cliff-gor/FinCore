using FinCore.SharedKernel.Integration;

namespace FinCore.BuildingBlocks.Application.EventBus;

/// <summary>Delivers integration events to subscribers. Called by the outbox processor, not by handlers directly.</summary>
public interface IEventBus
{
    Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken);
}
