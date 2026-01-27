using System.Collections.Concurrent;
using FinCore.Bff.Sessions;
using FinCore.BuildingBlocks.Infrastructure.Sessions;

namespace FinCore.Bff.Tests.Fakes;

/// <summary>In-memory <see cref="IBffSessionRepository"/> with the same semantics as the Postgres one.</summary>
internal sealed class InMemoryBffSessionRepository : IBffSessionRepository
{
    private readonly ConcurrentDictionary<string, BffSession> _sessions = new(StringComparer.Ordinal);

    public IReadOnlyCollection<BffSession> All => _sessions.Values.ToList();

    public BffSession? this[string id] => _sessions.TryGetValue(id, out var s) ? s : null;

    public Task AddAsync(BffSession session, CancellationToken cancellationToken)
    {
        _sessions[session.Id] = Copy(session);
        return Task.CompletedTask;
    }

    public Task<BffSession?> FindAsync(string id, CancellationToken cancellationToken) =>
        Task.FromResult(_sessions.TryGetValue(id, out var s) ? Copy(s) : null);

    public Task UpdateTicketAsync(string id, string protectedTokens, DateTime accessTokenExpiresAtUtc, DateTime lastSeenAtUtc, CancellationToken cancellationToken)
    {
        if (_sessions.TryGetValue(id, out var s))
        {
            s.ProtectedTokens = protectedTokens;
            s.AccessTokenExpiresAtUtc = accessTokenExpiresAtUtc;
            s.LastSeenAtUtc = lastSeenAtUtc;
        }

        return Task.CompletedTask;
    }

    public Task TouchAsync(string id, DateTime lastSeenAtUtc, CancellationToken cancellationToken)
    {
        if (_sessions.TryGetValue(id, out var s))
        {
            s.LastSeenAtUtc = lastSeenAtUtc;
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        _sessions.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    public Task<int> DeleteExpiredAsync(string mode, DateTime idleCutoffUtc, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var expired = _sessions.Values
            .Where(s => s.Mode == mode && (s.RevokedAtUtc != null || s.LastSeenAtUtc < idleCutoffUtc || s.AbsoluteExpiresAtUtc <= nowUtc))
            .Select(s => s.Id)
            .ToList();
        foreach (var id in expired)
        {
            _sessions.TryRemove(id, out _);
        }

        return Task.FromResult(expired.Count);
    }

    /// <summary>Marks a session revoked, as an operator's "sign out everywhere" would.</summary>
    public void Revoke(string id, DateTime atUtc) => _sessions[id].RevokedAtUtc = atUtc;

    // Callers get copies, like rows read from a database, so tests cannot pass by sharing references.
    private static BffSession Copy(BffSession s) => new()
    {
        Id = s.Id,
        Subject = s.Subject,
        Mode = s.Mode,
        ProtectedTokens = s.ProtectedTokens,
        AccessTokenExpiresAtUtc = s.AccessTokenExpiresAtUtc,
        CreatedAtUtc = s.CreatedAtUtc,
        LastSeenAtUtc = s.LastSeenAtUtc,
        AbsoluteExpiresAtUtc = s.AbsoluteExpiresAtUtc,
        RevokedAtUtc = s.RevokedAtUtc,
    };
}
