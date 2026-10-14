using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using FinCore.BuildingBlocks.Infrastructure.Sessions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace FinCore.Bff.Sessions;

/// <summary>
/// Server-side session store for the cookie handler (spec 002 "BFF sessions"). The browser cookie carries only a random
/// session id; the authentication ticket (principal plus access, refresh and id tokens) is serialized with
/// <see cref="TicketSerializer"/>, encrypted with Data Protection and kept in <c>platform.bff_sessions</c>, so tokens
/// never reach browser JavaScript and a session can be ended server-side.
/// </summary>
/// <remarks>
/// Lifetimes are enforced here, not trusted to the cookie: a session idle for longer than
/// <see cref="BffSessionOptions.IdleTimeout"/>, older than <see cref="BffSessionOptions.AbsoluteLifetime"/> or revoked
/// is deleted on its next use and the request is treated as signed out (spec 002 AC-3).
/// </remarks>
internal sealed partial class BffTicketStore(
    IBffSessionRepository sessions,
    IDataProtectionProvider dataProtection,
    IOptions<BffSessionOptions> options,
    TimeProvider timeProvider,
    ILogger<BffTicketStore> logger) : ITicketStore
{
    /// <summary>
    /// Ticket property holding the session id, so code that only has the ticket (token refresh) can find its row.
    /// It is inside the encrypted ticket, never sent to the browser on its own.
    /// </summary>
    public const string SessionIdItem = ".fincore.session-id";

    /// <summary>Saved-token name of the access token's expiry, as written by the OIDC handler with <c>SaveTokens</c>.</summary>
    public const string ExpiresAtToken = "expires_at";

    // Writing LastSeenAtUtc on every request would turn each proxied call into a database write. Recording activity at
    // most once a minute keeps the idle timeout accurate to that minute, erring on the strict side.
    private static readonly TimeSpan TouchInterval = TimeSpan.FromMinutes(1);

    // Versioned purpose: changing the ticket format later only needs a new purpose, old sessions then fail closed.
    private readonly IDataProtector _protector = dataProtection.CreateProtector("FinCore.Bff.SessionTicket.v1");
    private readonly BffSessionOptions _options = options.Value;

    public Task<string> StoreAsync(AuthenticationTicket ticket) => StoreAsync(ticket, CancellationToken.None);

    public async Task<string> StoreAsync(AuthenticationTicket ticket, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        var id = NewSessionId();
        ticket.Properties.Items[SessionIdItem] = id;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var session = new BffSession
        {
            Id = id,
            Subject = ticket.Principal.FindFirst("sub")?.Value
                ?? throw new InvalidOperationException("The identity provider returned no 'sub' claim."),
            Mode = _options.Mode,
            ProtectedTokens = Protect(ticket),
            AccessTokenExpiresAtUtc = AccessTokenExpiresAt(ticket.Properties) ?? now,
            CreatedAtUtc = now,
            LastSeenAtUtc = now,
            // The absolute limit is fixed at sign-in and never slides (10 h staff / 8 h customers).
            AbsoluteExpiresAtUtc = now + _options.AbsoluteLifetime,
        };

        await sessions.AddAsync(session, cancellationToken).ConfigureAwait(false);
        return id;
    }

    public Task<AuthenticationTicket?> RetrieveAsync(string key) => RetrieveAsync(key, CancellationToken.None);

    public async Task<AuthenticationTicket?> RetrieveAsync(string key, CancellationToken cancellationToken)
    {
        var session = await sessions.FindAsync(key, cancellationToken).ConfigureAwait(false);
        // A session of the other web app is ignored, not deleted: it is still valid in the app that created it.
        if (session is null || !string.Equals(session.Mode, _options.Mode, StringComparison.Ordinal))
        {
            return null;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (IsExpired(session, now))
        {
            await sessions.DeleteAsync(key, cancellationToken).ConfigureAwait(false);
            return null;
        }

        var ticket = Unprotect(session.ProtectedTokens);
        if (ticket is null)
        {
            // Unreadable (e.g. the Data Protection key ring was lost): fail closed and make the user sign in again.
            await sessions.DeleteAsync(key, cancellationToken).ConfigureAwait(false);
            return null;
        }

        if (now - session.LastSeenAtUtc >= TouchInterval)
        {
            await sessions.TouchAsync(key, now, cancellationToken).ConfigureAwait(false);
        }

        ticket.Properties.Items[SessionIdItem] = key;
        return ticket;
    }

    public Task RenewAsync(string key, AuthenticationTicket ticket) => RenewAsync(key, ticket, CancellationToken.None);

    public async Task RenewAsync(string key, AuthenticationTicket ticket, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        var session = await sessions.FindAsync(key, cancellationToken).ConfigureAwait(false);
        // Never resurrect a session that was signed out or expired while this request was running.
        if (session is null || !string.Equals(session.Mode, _options.Mode, StringComparison.Ordinal))
        {
            return;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var incomingExpiry = AccessTokenExpiresAt(ticket.Properties) ?? DateTime.MinValue;

        // The cookie handler's sliding renewal writes back the ticket this request loaded. If another request refreshed
        // the tokens meanwhile, that copy holds a refresh token the identity provider has already rotated out, and
        // storing it would end the session at the next refresh. Older tokens therefore only count as activity.
        if (incomingExpiry < session.AccessTokenExpiresAtUtc)
        {
            await sessions.TouchAsync(key, now, cancellationToken).ConfigureAwait(false);
            return;
        }

        ticket.Properties.Items[SessionIdItem] = key;
        await sessions.UpdateTicketAsync(key, Protect(ticket), incomingExpiry, now, cancellationToken).ConfigureAwait(false);
    }

    public Task RemoveAsync(string key) => RemoveAsync(key, CancellationToken.None);

    public Task RemoveAsync(string key, CancellationToken cancellationToken) => sessions.DeleteAsync(key, cancellationToken);

    /// <summary>Access-token expiry saved by the OIDC handler (<c>expires_at</c>, round-trip format), in UTC.</summary>
    public static DateTime? AccessTokenExpiresAt(AuthenticationProperties properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        return DateTimeOffset.TryParse(
            properties.GetTokenValue(ExpiresAtToken),
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var expiresAt)
            ? expiresAt.UtcDateTime
            : null;
    }

    private bool IsExpired(BffSession session, DateTime now) =>
        session.RevokedAtUtc is not null
        || now >= session.AbsoluteExpiresAtUtc
        || now - session.LastSeenAtUtc >= _options.IdleTimeout;

    // 256 bits from the OS CSPRNG, base64url: unguessable and safe in a cookie (43 characters).
    private static string NewSessionId() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    private string Protect(AuthenticationTicket ticket) =>
        Convert.ToBase64String(_protector.Protect(TicketSerializer.Default.Serialize(ticket)));

    private AuthenticationTicket? Unprotect(string protectedTicket)
    {
        try
        {
            return TicketSerializer.Default.Deserialize(_protector.Unprotect(Convert.FromBase64String(protectedTicket)));
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            LogUnreadableSession(logger, ex);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "A BFF session could not be decrypted and was deleted; the user must sign in again.")]
    private static partial void LogUnreadableSession(ILogger logger, Exception exception);
}
