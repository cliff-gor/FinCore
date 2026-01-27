using System.Collections.Concurrent;
using System.Globalization;
using FinCore.Bff.Sessions;
using Microsoft.AspNetCore.Authentication;

namespace FinCore.Bff.Tokens;

/// <summary>What the BFF can attach to a request on behalf of the browser.</summary>
internal enum AccessTokenStatus
{
    /// <summary>No session cookie, or the session expired: the request goes on without a token.</summary>
    NoSession,

    /// <summary><see cref="AccessTokenResult.AccessToken"/> is valid for at least another minute.</summary>
    Valid,

    /// <summary>The refresh token was rejected; the server-side session has been deleted.</summary>
    SessionEnded,

    /// <summary>The identity provider did not answer; the session is kept so the next request can retry.</summary>
    ProviderUnavailable,
}

internal sealed record AccessTokenResult(AccessTokenStatus Status, string? AccessToken = null)
{
    public static readonly AccessTokenResult NoSession = new(AccessTokenStatus.NoSession);
    public static readonly AccessTokenResult SessionEnded = new(AccessTokenStatus.SessionEnded);
    public static readonly AccessTokenResult ProviderUnavailable = new(AccessTokenStatus.ProviderUnavailable);
}

/// <summary>
/// Hands out the session's access token, refreshing it silently shortly before it expires (access tokens live 5 min,
/// spec 002 "BFF sessions").
/// </summary>
/// <remarks>
/// <para>
/// <b>Single flight.</b> A page load fires several <c>/api</c> calls at once. With refresh-token rotation each refresh
/// token is single-use, so parallel refreshes would race and all but one would be rejected — ending the session. All
/// requests of one session therefore share one in-flight refresh.
/// </para>
/// <para>
/// The in-flight map is per gateway instance. A second instance re-reads the stored ticket first (see
/// <see cref="RefreshCoreAsync"/>), so it normally finds the tokens already refreshed; a truly simultaneous refresh on
/// two instances can still end the session and the user signs in again — acceptable for M1's single instance.
/// </para>
/// </remarks>
internal sealed class SessionTokenService(BffTicketStore ticketStore, OidcClient oidc, TimeProvider timeProvider)
{
    /// <summary>Refresh this long before expiry, so a token never expires while a proxied call is in flight.</summary>
    public static readonly TimeSpan RefreshBeforeExpiry = TimeSpan.FromSeconds(60);

    // Bounds a refresh that is shared by several requests; it must not be cancelled by whichever request started it.
    private static readonly TimeSpan RefreshTimeout = TimeSpan.FromSeconds(15);

    private readonly ConcurrentDictionary<string, Lazy<Task<RefreshResult>>> _inFlight = new(StringComparer.Ordinal);

    /// <summary>Authenticates the session cookie and returns a fresh access token for it.</summary>
    public async Task<AccessTokenResult> GetAccessTokenAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Cached per request by the cookie handler, so this does not read the session row a second time.
        var auth = await context.AuthenticateAsync(BffAuthentication.CookieScheme).ConfigureAwait(false);
        if (!auth.Succeeded || auth.Properties is null)
        {
            return AccessTokenResult.NoSession;
        }

        return await EnsureFreshAsync(auth.Properties, context.RequestAborted).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns the access token in <paramref name="properties"/>, refreshing it first when it expires within
    /// <see cref="RefreshBeforeExpiry"/>. Refreshed tokens are written into <paramref name="properties"/> as well.
    /// </summary>
    public async Task<AccessTokenResult> EnsureFreshAsync(AuthenticationProperties properties, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(properties);

        var accessToken = properties.GetTokenValue("access_token");
        if (!properties.Items.TryGetValue(BffTicketStore.SessionIdItem, out var sessionId) || sessionId is null || accessToken is null)
        {
            return AccessTokenResult.NoSession;
        }

        if (!NeedsRefresh(BffTicketStore.AccessTokenExpiresAt(properties)))
        {
            return new AccessTokenResult(AccessTokenStatus.Valid, accessToken);
        }

        var result = await RefreshOnceAsync(sessionId).WaitAsync(cancellationToken).ConfigureAwait(false);
        switch (result.Status)
        {
            case RefreshStatus.Refreshed:
                // The cookie handler's sliding renewal writes this request's ticket back to the store; it must carry
                // the new tokens, or the rotated-out refresh token would be saved again.
                Apply(result.Tokens!, properties);
                return new AccessTokenResult(AccessTokenStatus.Valid, result.Tokens!.AccessToken);
            case RefreshStatus.Unavailable:
                return AccessTokenResult.ProviderUnavailable;
            default:
                // Spec 002: when the refresh fails the session ends; the caller answers 401 and the SPA signs in again.
                await ticketStore.RemoveAsync(sessionId, cancellationToken).ConfigureAwait(false);
                return AccessTokenResult.SessionEnded;
        }
    }

    private bool NeedsRefresh(DateTime? accessTokenExpiresAtUtc) =>
        accessTokenExpiresAtUtc is null
        || accessTokenExpiresAtUtc.Value - timeProvider.GetUtcNow().UtcDateTime <= RefreshBeforeExpiry;

    private async Task<RefreshResult> RefreshOnceAsync(string sessionId)
    {
        // Lazy makes the factory run once even when GetOrAdd races; every caller then awaits the same task.
        var flight = _inFlight.GetOrAdd(sessionId, id => new Lazy<Task<RefreshResult>>(() => RefreshCoreAsync(id)));
        try
        {
            return await flight.Value.ConfigureAwait(false);
        }
        finally
        {
            // Removes only this flight; a newer one for the same session (started after completion) is left alone.
            _inFlight.TryRemove(KeyValuePair.Create(sessionId, flight));
        }
    }

    private async Task<RefreshResult> RefreshCoreAsync(string sessionId)
    {
        using var timeout = new CancellationTokenSource(RefreshTimeout);

        // Re-read the stored ticket instead of trusting the copy the request loaded: another request or gateway
        // instance may have refreshed already, and with rotation the loaded refresh token is then spent.
        var ticket = await ticketStore.RetrieveAsync(sessionId, timeout.Token).ConfigureAwait(false);
        if (ticket is null)
        {
            return new RefreshResult(RefreshStatus.Rejected);
        }

        var stored = ticket.Properties;
        if (!NeedsRefresh(BffTicketStore.AccessTokenExpiresAt(stored)) && stored.GetTokenValue("access_token") is { } current)
        {
            return new RefreshResult(
                RefreshStatus.Refreshed,
                new TokenSet(current, stored.GetTokenValue("refresh_token"), stored.GetTokenValue("id_token"), BffTicketStore.AccessTokenExpiresAt(stored)!.Value));
        }

        if (stored.GetTokenValue("refresh_token") is not { Length: > 0 } refreshToken)
        {
            return new RefreshResult(RefreshStatus.Rejected);
        }

        var result = await oidc.RefreshAsync(refreshToken, timeout.Token).ConfigureAwait(false);
        if (result.Status != RefreshStatus.Refreshed)
        {
            return result;
        }

        // Providers may omit a new refresh or id token; the previous ones then stay valid and are kept.
        var tokens = result.Tokens! with
        {
            RefreshToken = result.Tokens!.RefreshToken ?? refreshToken,
            IdToken = result.Tokens.IdToken ?? stored.GetTokenValue("id_token"),
        };
        Apply(tokens, stored);
        await ticketStore.RenewAsync(sessionId, ticket, timeout.Token).ConfigureAwait(false);
        return new RefreshResult(RefreshStatus.Refreshed, tokens);
    }

    private static void Apply(TokenSet tokens, AuthenticationProperties properties)
    {
        var updated = properties.GetTokens()
            .Where(t => t.Name is not ("access_token" or "refresh_token" or "id_token" or BffTicketStore.ExpiresAtToken))
            .ToList();
        updated.Add(new AuthenticationToken { Name = "access_token", Value = tokens.AccessToken });
        updated.Add(new AuthenticationToken
        {
            Name = BffTicketStore.ExpiresAtToken,
            Value = tokens.ExpiresAtUtc.ToString("o", CultureInfo.InvariantCulture),
        });
        if (tokens.RefreshToken is not null)
        {
            updated.Add(new AuthenticationToken { Name = "refresh_token", Value = tokens.RefreshToken });
        }

        if (tokens.IdToken is not null)
        {
            updated.Add(new AuthenticationToken { Name = "id_token", Value = tokens.IdToken });
        }

        properties.StoreTokens(updated);
    }
}
