using System.Net.Http.Headers;
using FinCore.Bff.Sessions;
using FinCore.Bff.Tokens;
using Microsoft.AspNetCore.Authentication;

namespace FinCore.Bff.Security;

/// <summary>
/// Web modes: attaches the session's access token to every <c>/api</c> request before YARP proxies it, so the browser
/// only ever holds the session cookie (master spec §30.3, spec 002 AC-3).
/// </summary>
/// <remarks>
/// Requests without a session are still proxied without a token: the API then answers 401 itself, so there is one
/// source of truth for "is this endpoint public". When the refresh token is rejected the session has ended and the BFF
/// answers 401 directly, clearing the cookie.
/// </remarks>
internal sealed class BffAccessTokenMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, SessionTokenService tokens)
    {
        if (!context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        // The only credential the API may see from a browser is the one the BFF attaches; a header the page set
        // itself is dropped so script can never choose the identity the API acts on.
        context.Request.Headers.Authorization = default;

        var token = await tokens.GetAccessTokenAsync(context).ConfigureAwait(false);
        switch (token.Status)
        {
            case AccessTokenStatus.Valid:
                context.Request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken).ToString();
                break;
            case AccessTokenStatus.SessionEnded:
                await context.SignOutAsync(BffAuthentication.CookieScheme).ConfigureAwait(false);
                await BffEndpoints.NotSignedIn().ExecuteAsync(context).ConfigureAwait(false);
                return;
            case AccessTokenStatus.ProviderUnavailable:
                await BffEndpoints.ProviderUnavailable().ExecuteAsync(context).ConfigureAwait(false);
                return;
            case AccessTokenStatus.NoSession:
            default:
                break;
        }

        await next(context).ConfigureAwait(false);
    }
}
