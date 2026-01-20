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
// DisallowConcurrentExecution: a slow batch must finish before the next trigger fires, otherwise two runs could
// publish the same message. The clustered Quartz Postgres store (spec §33.5) extends this across API instances.
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
            // Oldest first, so subscribers see a module's events in the order they happened.
            .OrderBy(m => m.OccurredOnUtc)
            .Take(settings.BatchSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var message in messages)
        {
            await ProcessAsync(message, settings, ct).ConfigureAwait(false);
        }

        // One save per batch records both successes and failure bookkeeping. If the process dies before this, the
        // batch is re-published; subscribers' inbox records make that harmless.
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
            // The stored type name lets us deserialize without a central event registry. Renaming an integration event
            // therefore needs a versioned name (spec §10), never a silent rename.
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

            // Park the message after MaxAttempts rather than retrying forever, so one poison message cannot block
            // monitoring or hide newer failures (spec 001: 5 attempts).
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
