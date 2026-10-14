namespace FinCore.Bff.Security;

/// <summary>
/// Web modes: every <c>/api</c> call and every state-changing <c>/bff</c> call (<c>POST /bff/logout</c>) must carry
/// <c>X-CSRF: 1</c>. Browsers cannot add custom headers to cross-site requests without CORS, so together with
/// SameSite cookies this blocks CSRF (spec §30.3). <c>GET /bff/login</c> is exempt: it is a top-level navigation, which
/// cannot carry headers, and it changes nothing until the user signs in at the identity provider.
/// </summary>
internal sealed class CsrfHeaderMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-CSRF";

    public async Task InvokeAsync(HttpContext context)
    {
        if (RequiresHeader(context.Request) && context.Request.Headers[HeaderName] != "1")
        {
            await Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Missing anti-forgery header",
                detail: $"Requests to /api and state-changing /bff endpoints must include the '{HeaderName}: 1' header.",
                extensions: new Dictionary<string, object?> { ["code"] = "Bff.CsrfHeaderMissing" })
                .ExecuteAsync(context).ConfigureAwait(false);
            return;
        }

        await next(context).ConfigureAwait(false);
    }

    private static bool RequiresHeader(HttpRequest request) =>
        request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
        || (request.Path.StartsWithSegments("/bff", StringComparison.OrdinalIgnoreCase)
            && !HttpMethods.IsGet(request.Method)
            && !HttpMethods.IsHead(request.Method));
}
