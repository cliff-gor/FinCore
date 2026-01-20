namespace FinCore.Bff.Security;

/// <summary>
/// Web modes: every <c>/api</c> call must carry <c>X-CSRF: 1</c>. Browsers cannot add custom headers to
/// cross-site requests without CORS, so together with SameSite cookies this blocks CSRF (spec §30.3).
/// </summary>
internal sealed class CsrfHeaderMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-CSRF";

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
            && context.Request.Headers[HeaderName] != "1")
        {
            await Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Missing anti-forgery header",
                detail: $"Requests to /api must include the '{HeaderName}: 1' header.",
                extensions: new Dictionary<string, object?> { ["code"] = "Bff.CsrfHeaderMissing" })
                .ExecuteAsync(context).ConfigureAwait(false);
            return;
        }

        await next(context).ConfigureAwait(false);
    }
}
