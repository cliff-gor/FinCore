using FinCore.Api.Platform;

namespace FinCore.Api.Middleware;

/// <summary>Adds <c>X-FinCore-Version</c> to every response (spec §33.7.3).</summary>
internal sealed class VersionHeaderMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-FinCore-Version";

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = BuildInfo.Product;
            return Task.CompletedTask;
        });
        return next(context);
    }
}
