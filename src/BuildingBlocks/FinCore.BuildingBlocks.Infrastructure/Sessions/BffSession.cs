namespace FinCore.BuildingBlocks.Infrastructure.Sessions;

/// <summary>
/// A signed-in browser session held by the BFF (spec §30.3, spec 002). The browser cookie carries only <see cref="Id"/>;
/// the OIDC tokens live here, encrypted with Data Protection, so they never reach JavaScript and can be revoked
/// server-side.
/// </summary>
public sealed class BffSession
{
    /// <summary>Random, unguessable session id (also the cookie value).</summary>
    public required string Id { get; init; }

    /// <summary>The identity provider's <c>sub</c> claim, so all sessions of a user can be revoked together.</summary>
    public required string Subject { get; init; }

    /// <summary>Gateway mode that created the session (BackOffice or Portal); sessions never cross apps.</summary>
    public required string Mode { get; init; }

    /// <summary>Data-Protection-encrypted JSON with the access, refresh and id tokens.</summary>
    public required string ProtectedTokens { get; set; }

    public DateTime AccessTokenExpiresAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; init; }

    /// <summary>Sliding idle timeout is measured from here (30 min staff / 15 min customers).</summary>
    public DateTime LastSeenAtUtc { get; set; }

    /// <summary>Hard limit regardless of activity (10 h staff / 8 h customers).</summary>
    public DateTime AbsoluteExpiresAtUtc { get; init; }

    public DateTime? RevokedAtUtc { get; set; }
}
