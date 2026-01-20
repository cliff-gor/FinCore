using FinCore.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace FinCore.BuildingBlocks.Infrastructure.Outbox;

/// <summary>Degraded when a module's pending outbox backlog exceeds the configured threshold.</summary>
internal sealed class OutboxBacklogHealthCheck<TDbContext>(TDbContext dbContext, IOptions<OutboxOptions> options) : IHealthCheck
    where TDbContext : ModuleDbContext
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var pending = await dbContext.OutboxMessages
            .CountAsync(m => m.ProcessedOnUtc == null && m.DeadLetteredOnUtc == null, cancellationToken)
            .ConfigureAwait(false);

        return pending > options.Value.BacklogDegradedThreshold
            ? HealthCheckResult.Degraded($"{pending} pending outbox messages in '{dbContext.Schema}'.")
            : HealthCheckResult.Healthy($"{pending} pending outbox messages in '{dbContext.Schema}'.");
    }
}
