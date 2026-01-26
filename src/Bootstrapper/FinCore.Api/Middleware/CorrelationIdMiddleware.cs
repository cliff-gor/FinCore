using System.Diagnostics;
using FinCore.BuildingBlocks.Web.Security;
using Serilog.Context;

namespace FinCore.Api.Middleware;

/// <summary>Echoes or creates <c>X-Correlation-Id</c> and attaches it to every log line of the request (spec §27, §28).</summary>
internal sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";
    private const int MaxLength = 128;

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].ToString();
        // Accept the caller's id so a request can be followed across systems, but cap its length: the header is
        // client-controlled and ends up in every log line.
        var correlationId = !string.IsNullOrWhiteSpace(incoming) && incoming.Length <= MaxLength
            ? incoming
            : Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });
        Activity.Current?.SetTag("fincore.correlation_id", correlationId);
        // Shared with the audit log through IRequestContext.
        context.Items[HttpRequestContext.CorrelationIdItem] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context).ConfigureAwait(false);
        }
    }
}
