using System.Collections.Concurrent;
using FinCore.BuildingBlocks.Application.EventBus;
using FinCore.SharedKernel.Integration;
using Microsoft.Extensions.DependencyInjection;

namespace FinCore.BuildingBlocks.Infrastructure.EventBus;

/// <summary>
/// In-process bus. Each subscriber runs in its own DI scope (so each uses its own module DbContext and inbox).
/// Failures are collected and rethrown after all subscribers ran, so the outbox retries the message; the inbox
/// keeps subscribers that already succeeded from repeating their work (ADR-0003). Swappable for a broker later (§36).
/// </summary>
internal sealed class InMemoryEventBus(IServiceScopeFactory scopeFactory) : IEventBus
{
    private static readonly ConcurrentDictionary<Type, Type> HandlerTypes = new();

    public async Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var handlerType = HandlerTypes.GetOrAdd(
            integrationEvent.GetType(),
            static t => typeof(IIntegrationEventHandler<>).MakeGenericType(t));
        var handleMethod = handlerType.GetMethod(nameof(IIntegrationEventHandler<IIntegrationEvent>.Handle))!;

        int handlerCount;
        await using (var probe = scopeFactory.CreateAsyncScope())
        {
            handlerCount = probe.ServiceProvider.GetServices(handlerType).Count();
        }

        List<Exception>? failures = null;
        for (var i = 0; i < handlerCount; i++)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetServices(handlerType).ElementAt(i)!;
            try
            {
                await ((Task)handleMethod.Invoke(handler, [integrationEvent, cancellationToken])!).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Every subscriber must get its turn; failures are rethrown below.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                (failures ??= []).Add(ex);
            }
        }

        if (failures is { Count: > 0 })
        {
            throw new AggregateException($"{failures.Count} subscriber(s) failed for {integrationEvent.GetType().Name}.", failures);
        }
    }
}
