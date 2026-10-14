using FinCore.BuildingBlocks.Infrastructure.Idempotency;
using FinCore.BuildingBlocks.Infrastructure.Sessions;
using Microsoft.EntityFrameworkCore;

namespace FinCore.Bff.Sessions;

/// <summary>
/// <see cref="IBffSessionRepository"/> on <c>platform.bff_sessions</c>. Sessions live in Postgres rather than in memory
/// so they survive gateway restarts, work across several gateway instances and can be revoked server-side (§30.3, §33.5).
/// </summary>
/// <remarks>
/// Singleton (the cookie handler's ticket store is a single instance), so each call opens its own DI scope for a fresh
/// <see cref="PlatformDbContext"/>. Updates and deletes are single set-based statements: no change tracking, and two
/// requests of the same session never fail each other with concurrency errors.
/// </remarks>
internal sealed class PostgresBffSessionRepository(IServiceScopeFactory scopeFactory) : IBffSessionRepository
{
    public async Task AddAsync(BffSession session, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.BffSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<BffSession?> FindAsync(string id, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return await db.BffSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateTicketAsync(string id, string protectedTokens, DateTime accessTokenExpiresAtUtc, DateTime lastSeenAtUtc, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await db.BffSessions
            .Where(s => s.Id == id)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(s => s.ProtectedTokens, protectedTokens)
                    .SetProperty(s => s.AccessTokenExpiresAtUtc, accessTokenExpiresAtUtc)
                    .SetProperty(s => s.LastSeenAtUtc, lastSeenAtUtc),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task TouchAsync(string id, DateTime lastSeenAtUtc, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await db.BffSessions
            .Where(s => s.Id == id)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.LastSeenAtUtc, lastSeenAtUtc), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await db.BffSessions.Where(s => s.Id == id).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> DeleteExpiredAsync(string mode, DateTime idleCutoffUtc, DateTime nowUtc, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return await db.BffSessions
            .Where(s => s.Mode == mode
                && (s.RevokedAtUtc != null || s.LastSeenAtUtc < idleCutoffUtc || s.AbsoluteExpiresAtUtc <= nowUtc))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
