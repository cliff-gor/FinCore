using System.Net.Http.Headers;
using FinCore.Bff.Sessions;
using FinCore.Bff.Tokens;
using Microsoft.AspNetCore.Authentication;

namespace FinCore.Bff;

/// <summary>
/// BFF session endpoints for the web modes (master spec §30.3, spec 002 "BFF (Back Office, Portal)"):
/// <c>GET /bff/login</c>, <c>GET /bff/user</c> and <c>POST /bff/logout</c>. <c>/signin-oidc</c> is handled by the
/// OpenID Connect middleware itself.
/// </summary>
internal static class BffEndpoints
{
    /// <summary>Named HttpClient that calls FinCore.Api directly (for <c>/api/v1/me</c>).</summary>
    public const string ApiClientName = "fincore-api";

    public static IEndpointRouteBuilder MapBffEndpoints(this IEndpointRouteBuilder app)
    {
        var bff = app.MapGroup("/bff");

        bff.MapGet("/login", (string? returnUrl) => Results.Challenge(
                new AuthenticationProperties { RedirectUri = SafeReturnUrl(returnUrl) },
                [BffAuthentication.OidcScheme]))
            .RequireRateLimiting(BffAuthentication.LoginRateLimitPolicy);

        bff.MapGet("/user", GetUserAsync);
        bff.MapPost("/logout", LogoutAsync);

        return app;
    }

    /// <summary>
    /// The signed-in user as FinCore.Api's <c>/api/v1/me</c> describes them (staff or customer shape). The API's 403
    /// ProblemDetails (<c>Identity.StaffNotRegistered</c>, <c>Identity.EmailNotVerified</c>, <c>Identity.StaffInactive</c>)
    /// are passed through unchanged so the app can explain why access is refused (spec 002 Edge Cases).
    /// </summary>
    private static async Task<IResult> GetUserAsync(HttpContext context, SessionTokenService tokens, IHttpClientFactory httpClientFactory)
    {
        // The answer is per user and changes on sign-in/out; no cache may keep it.
        context.Response.Headers.CacheControl = "no-store";

        var token = await tokens.GetAccessTokenAsync(context).ConfigureAwait(false);
        switch (token.Status)
        {
            case AccessTokenStatus.Valid:
                break;
            case AccessTokenStatus.ProviderUnavailable:
                return ProviderUnavailable();
            case AccessTokenStatus.SessionEnded:
                await context.SignOutAsync(BffAuthentication.CookieScheme).ConfigureAwait(false);
                return NotSignedIn();
            default:
                return NotSignedIn();
        }

        using var client = httpClientFactory.CreateClient(ApiClientName);
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        using var response = await client.SendAsync(request, context.RequestAborted).ConfigureAwait(false);

        var body = await response.Content.ReadAsStringAsync(context.RequestAborted).ConfigureAwait(false);
        return Results.Content(
            body,
            response.Content.Headers.ContentType?.ToString() ?? "application/json",
            statusCode: (int)response.StatusCode);
    }

    /// <summary>
    /// Ends the session: revokes the refresh token at the identity provider, deletes the server-side session and the
    /// cookie, and returns the provider's end-session URL for the SPA to navigate to (so the SSO session ends too).
    /// Requires <c>X-CSRF: 1</c> (<see cref="Security.CsrfHeaderMiddleware"/>), so another site cannot sign the user out.
    /// </summary>
    private static async Task<IResult> LogoutAsync(HttpContext context, OidcClient oidc)
    {
        var logoutUrl = "/";
        var auth = await context.AuthenticateAsync(BffAuthentication.CookieScheme).ConfigureAwait(false);
        if (auth.Succeeded && auth.Properties is { } properties)
        {
            if (properties.GetTokenValue("refresh_token") is { Length: > 0 } refreshToken)
            {
                await oidc.RevokeRefreshTokenAsync(refreshToken, context.RequestAborted).ConfigureAwait(false);
            }

            // Must be registered as a post-logout redirect URI of the client (Keycloak: https://localhost:720x/*).
            var postLogoutRedirectUri = $"{context.Request.Scheme}://{context.Request.Host}/";
            logoutUrl = await oidc
                .EndSessionUrlAsync(properties.GetTokenValue("id_token"), postLogoutRedirectUri, context.RequestAborted)
                .ConfigureAwait(false) ?? "/";
        }

        // Deletes the session row through the ticket store and expires the cookie, also when no session was found.
        await context.SignOutAsync(BffAuthentication.CookieScheme).ConfigureAwait(false);
        context.Response.Headers.CacheControl = "no-store";
        return Results.Ok(new LogoutResponse(logoutUrl));
    }

    /// <summary>Only local paths are allowed, preventing open redirects (<c>//evil.com</c>, <c>https://…</c>).</summary>
    internal static string SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl)
        && returnUrl.StartsWith('/')
        && !returnUrl.StartsWith("//", StringComparison.Ordinal)
        && !returnUrl.StartsWith("/\\", StringComparison.Ordinal)
            ? returnUrl
            : "/";

    internal static IResult NotSignedIn() => Results.Problem(
        statusCode: StatusCodes.Status401Unauthorized,
        title: "Not signed in",
        extensions: new Dictionary<string, object?> { ["code"] = "Bff.NotSignedIn" });

    internal static IResult ProviderUnavailable() => Results.Problem(
        statusCode: StatusCodes.Status503ServiceUnavailable,
        title: "Sign-in service unavailable",
        detail: "The identity provider did not answer. Please try again shortly.",
        extensions: new Dictionary<string, object?> { ["code"] = "Bff.IdentityProviderUnavailable" });

    /// <summary>Body of <c>POST /bff/logout</c>.</summary>
    internal sealed record LogoutResponse(string LogoutUrl);
}
