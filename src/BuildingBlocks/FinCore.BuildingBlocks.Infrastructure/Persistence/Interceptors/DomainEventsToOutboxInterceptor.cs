using System.Diagnostics;
using System.Text.Json;
using FinCore.BuildingBlocks.Infrastructure.EventBus;
using FinCore.BuildingBlocks.Infrastructure.Outbox;
using FinCore.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FinCore.BuildingBlocks.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Maps aggregates' domain events to integration events and stores them in the module's outbox
/// in the same transaction as the state change (spec §10).
/// </summary>
public sealed class DomainEventsToOutboxInterceptor(IDomainEventMapperRegistry mappers) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context is { } context)
        {
            WriteOutbox(context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void WriteOutbox(DbContext context)
    {
        var aggregates = context.ChangeTracker.Entries<IAggregateRoot>()
            .Select(e => e.Entity)
            .Where(a => a.DomainEvents.Count > 0)
            .ToList();

        // Capture the current trace so the asynchronous publish later appears in the same distributed trace (spec §27).
        var traceParent = Activity.Current?.Id;

        foreach (var aggregate in aggregates)
        {
            foreach (var domainEvent in aggregate.DomainEvents)
            {
                var integrationEvent = mappers.Map(domainEvent);
                // Events without a mapper are internal to the module and never leave it.
                if (integrationEvent is null)
                {
                    continue;
                }

                var type = integrationEvent.GetType();
                context.Set<OutboxMessage>().Add(new OutboxMessage
                {
                    Id = integrationEvent.Id,
                    Type = $"{type.FullName}, {type.Assembly.GetName().Name}",
                    Content = JsonSerializer.Serialize(integrationEvent, type, OutboxSerializer.Options),
                    OccurredOnUtc = integrationEvent.OccurredOnUtc,
                    TraceParent = traceParent,
                });
            }

            // Clear after staging so a second SaveChanges in the same unit of work cannot enqueue duplicates.
            aggregate.ClearDomainEvents();
        }
    }
}
