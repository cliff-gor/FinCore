namespace FinCore.Bff;

/// <summary>
/// BFF session endpoints. M0 stubs (spec 001): no OIDC yet, so <c>/bff/user</c> is always 401 and login just
/// returns to the app. Real cookie + OIDC sessions with server-side tokens arrive in M1.
/// </summary>
internal static class BffEndpoints
{
    public static IEndpointRouteBuilder MapBffEndpoints(this IEndpointRouteBuilder app)
    {
        var bff = app.MapGroup("/bff");

        // TODO(M1): return the signed-in user's claims, permissions, institution and business date from the session.
        bff.MapGet("/user", () => Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Not signed in",
            extensions: new Dictionary<string, object?> { ["code"] = "Bff.NotSignedIn" }));

        bff.MapGet("/login", (string? returnUrl) => Results.Redirect(SafeReturnUrl(returnUrl)));

        bff.MapPost("/logout", () => Results.NoContent());

        return app;
    }

    /// <summary>Only local paths are allowed, preventing open redirects (<c>//evil.com</c>, <c>https://…</c>).</summary>
    internal static string SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl)
        && returnUrl.StartsWith('/')
        && !returnUrl.StartsWith("//", StringComparison.Ordinal)
        && !returnUrl.StartsWith("/\\", StringComparison.Ordinal)
            ? returnUrl
            : "/";
}
