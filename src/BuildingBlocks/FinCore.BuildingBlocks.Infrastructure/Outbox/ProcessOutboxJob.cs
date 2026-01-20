using System.Diagnostics;
using System.Text.Json;
using FinCore.BuildingBlocks.Application.EventBus;
using FinCore.BuildingBlocks.Infrastructure.EventBus;
using FinCore.BuildingBlocks.Infrastructure.Persistence;
using FinCore.SharedKernel.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace FinCore.BuildingBlocks.Infrastructure.Outbox;

/// <summary>Publishes a module's pending outbox messages to the event bus, with retry and dead-lettering.</summary>
[DisallowConcurrentExecution]
public sealed class ProcessOutboxJob<TDbContext>(
    TDbContext dbContext,
    IEventBus eventBus,
    TimeProvider timeProvider,
    IOptions<OutboxOptions> options,
    OutboxMetrics metrics,
    ILogger<ProcessOutboxJob<TDbContext>> logger) : IJob
    where TDbContext : ModuleDbContext
{
    private static readonly ActivitySource ActivitySource = new("FinCore.Outbox");

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var ct = cancellationToken;
        var settings = options.Value;
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var messages = await dbContext.OutboxMessages
            .Where(m => m.ProcessedOnUtc == null && m.DeadLetteredOnUtc == null
                        && (m.NextAttemptOnUtc == null || m.NextAttemptOnUtc <= now))
            .OrderBy(m => m.OccurredOnUtc)
            .Take(settings.BatchSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var message in messages)
        {
            await ProcessAsync(message, settings, ct).ConfigureAwait(false);
        }

        if (messages.Count > 0)
        {
            await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        var pending = await dbContext.OutboxMessages
            .LongCountAsync(m => m.ProcessedOnUtc == null && m.DeadLetteredOnUtc == null, ct)
            .ConfigureAwait(false);
        metrics.RecordPending(dbContext.Schema, pending);
    }

    private async Task ProcessAsync(OutboxMessage message, OutboxOptions settings, CancellationToken ct)
    {
        using var activity = ActivitySource.StartActivity(
            "outbox.publish", ActivityKind.Producer, message.TraceParent);
        activity?.SetTag("messaging.message.id", message.Id);

        try
        {
            var type = System.Type.GetType(message.Type, throwOnError: true)!;
            var integrationEvent = (IIntegrationEvent)JsonSerializer.Deserialize(message.Content, type, OutboxSerializer.Options)!;

            await eventBus.PublishAsync(integrationEvent, ct).ConfigureAwait(false);

            message.ProcessedOnUtc = timeProvider.GetUtcNow().UtcDateTime;
            message.Error = null;
        }
#pragma warning disable CA1031 // A failing message must not stop the batch; it is retried or dead-lettered.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            message.Attempts++;
            message.Error = ex.ToString();
            var now = timeProvider.GetUtcNow().UtcDateTime;

            if (message.Attempts >= settings.MaxAttempts)
            {
                message.DeadLetteredOnUtc = now;
                metrics.RecordDeadLettered(dbContext.Schema);
                logger.LogError(ex, "Outbox message {MessageId} ({MessageType}) dead-lettered after {Attempts} attempts", message.Id, message.Type, message.Attempts);
            }
            else
            {
                message.NextAttemptOnUtc = now + (settings.BaseRetryDelay * Math.Pow(2, message.Attempts - 1));
                logger.LogWarning(ex, "Outbox message {MessageId} failed (attempt {Attempts}); retrying at {NextAttempt}", message.Id, message.Attempts, message.NextAttemptOnUtc);
            }
        }
    }
}
