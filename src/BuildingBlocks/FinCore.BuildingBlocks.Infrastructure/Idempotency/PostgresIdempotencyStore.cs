using FinCore.BuildingBlocks.Application.Idempotency;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FinCore.BuildingBlocks.Infrastructure.Idempotency;

/// <summary>Idempotency store in Postgres (spec §33.5: no Redis on AWS). Records live 24 hours.</summary>
internal sealed class PostgresIdempotencyStore(PlatformDbContext dbContext, TimeProvider timeProvider) : IIdempotencyStore
{
    public static readonly TimeSpan Retention = TimeSpan.FromHours(24);

    public async Task<IdempotentResponse?> GetAsync(string scopedKey, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var record = await dbContext.IdempotencyRecords.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Key == scopedKey && r.ExpiresAtUtc > now, cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : new IdempotentResponse(record.RequestHash, record.StatusCode, record.ContentType, record.Body);
    }

    public async Task SaveAsync(string scopedKey, IdempotentResponse response, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);

        var now = timeProvider.GetUtcNow().UtcDateTime;

        // An expired record with the same key may still exist until the purge job runs.
        await dbContext.IdempotencyRecords.Where(r => r.Key == scopedKey && r.ExpiresAtUtc <= now)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        dbContext.IdempotencyRecords.Add(new IdempotencyRecord
        {
            Key = scopedKey,
            RequestHash = response.RequestHash,
            StatusCode = response.StatusCode,
            ContentType = response.ContentType,
            Body = response.Body,
            CreatedAtUtc = now,
            ExpiresAtUtc = now + Retention,
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // A concurrent request with the same key stored its response first; keep that one.
            dbContext.ChangeTracker.Clear();
        }
    }
}

