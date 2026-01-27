using FinCore.BuildingBlocks.Infrastructure.Sessions;

namespace FinCore.Bff.Sessions;

/// <summary>
/// Persistence of <see cref="BffSession"/> rows. Kept separate from <see cref="BffTicketStore"/> so the session rules
/// (expiry, revocation, never storing older tokens) are tested without a database.
/// </summary>
internal interface IBffSessionRepository
{
    Task AddAsync(BffSession session, CancellationToken cancellationToken);

    Task<BffSession?> FindAsync(string id, CancellationToken cancellationToken);

    /// <summary>Replaces the protected ticket after a token refresh or a sliding renewal.</summary>
    Task UpdateTicketAsync(string id, string protectedTokens, DateTime accessTokenExpiresAtUtc, DateTime lastSeenAtUtc, CancellationToken cancellationToken);

    /// <summary>Moves the idle-timeout clock forward without touching the tokens.</summary>
    Task TouchAsync(string id, DateTime lastSeenAtUtc, CancellationToken cancellationToken);

    Task DeleteAsync(string id, CancellationToken cancellationToken);

    /// <summary>Deletes this mode's sessions that are revoked, idle since before <paramref name="idleCutoffUtc"/> or past their absolute expiry.</summary>
    Task<int> DeleteExpiredAsync(string mode, DateTime idleCutoffUtc, DateTime nowUtc, CancellationToken cancellationToken);
}
