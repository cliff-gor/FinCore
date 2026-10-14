namespace FinCore.Bff.Security;

/// <summary>Strict security headers on every gateway response (spec §30.3).</summary>
internal sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    /// <summary>
    /// <c>HttpContext.Items</c> flag set for responses of the Development-only SPA dev-server route (see
    /// <c>DevSpaProxy</c>), which need a looser policy.
    /// </summary>
    public const string DevSpaResponseItem = "fincore.dev-spa";

    private const string StrictPolicy = "default-src 'self'; frame-ancestors 'none'; object-src 'none'; base-uri 'self'";

    // Vite's dev server injects inline scripts (React Fast Refresh preamble) and inline styles, and keeps a WebSocket
    // open for hot reload. Only ever applied in Development; built SPAs are served under the strict policy.
    private const string DevSpaPolicy =
        "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; " +
        "connect-src 'self' ws: wss:; frame-ancestors 'none'; object-src 'none'; base-uri 'self'";

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["Referrer-Policy"] = "no-referrer";
            headers["X-Frame-Options"] = "DENY";
            headers["Content-Security-Policy"] = context.Items.ContainsKey(DevSpaResponseItem) ? DevSpaPolicy : StrictPolicy;
            return Task.CompletedTask;
        });
        return next(context);
    }
}
