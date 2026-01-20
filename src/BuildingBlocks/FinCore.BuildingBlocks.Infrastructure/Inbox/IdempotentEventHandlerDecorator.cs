using FinCore.BuildingBlocks.Application.EventBus;
using FinCore.BuildingBlocks.Infrastructure.Persistence;
using FinCore.SharedKernel.Integration;
using Microsoft.EntityFrameworkCore;

namespace FinCore.BuildingBlocks.Infrastructure.Inbox;

/// <summary>
/// Wraps a subscriber so each message is handled at most once per handler: the handler's work and its
/// inbox row commit in one transaction of the subscriber module's DbContext.
/// </summary>
internal sealed class IdempotentEventHandlerDecorator<TEvent, TDbContext>(
    IIntegrationEventHandler<TEvent> inner,
    TDbContext dbContext,
    TimeProvider timeProvider) : IIntegrationEventHandler<TEvent>
    where TEvent : IIntegrationEvent
    where TDbContext : ModuleDbContext
{
    private readonly string _handlerName = inner.GetType().FullName!;

    public async Task Handle(TEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var alreadyHandled = await dbContext.InboxMessages
            .AnyAsync(m => m.MessageId == integrationEvent.Id && m.HandlerName == _handlerName, cancellationToken)
            .ConfigureAwait(false);
        if (alreadyHandled)
        {
            return;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await inner.Handle(integrationEvent, cancellationToken).ConfigureAwait(false);

        dbContext.InboxMessages.Add(new InboxMessage
        {
            MessageId = integrationEvent.Id,
            HandlerName = _handlerName,
            ProcessedOnUtc = timeProvider.GetUtcNow().UtcDateTime,
        });
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
