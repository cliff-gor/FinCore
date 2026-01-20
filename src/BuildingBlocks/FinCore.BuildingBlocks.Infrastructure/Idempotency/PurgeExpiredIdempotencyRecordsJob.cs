using Microsoft.EntityFrameworkCore;
using Quartz;

namespace FinCore.BuildingBlocks.Infrastructure.Idempotency;

/// <summary>Deletes idempotency records past their 24-hour retention (hourly).</summary>
[DisallowConcurrentExecution]
internal sealed class PurgeExpiredIdempotencyRecordsJob(PlatformDbContext dbContext, TimeProvider timeProvider) : IJob
{
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        await dbContext.IdempotencyRecords.Where(r => r.ExpiresAtUtc <= now)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
    }
}
