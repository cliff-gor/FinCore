using System.Security.Cryptography;
using System.Text.Json;
using FinCore.BuildingBlocks.Application.Idempotency;
using FinCore.BuildingBlocks.Application.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace FinCore.BuildingBlocks.Web;

/// <summary>
/// Makes an endpoint idempotent via the <c>Idempotency-Key</c> header (spec §25): first call stores the response;
/// a replay with the same body returns it; same key with a different body → 422; missing key → 400.
/// </summary>
public sealed class IdempotencyFilter : IEndpointFilter
{
    public const string HeaderName = "Idempotency-Key";
    private const int MaxKeyLength = 100;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var http = context.HttpContext;
        var key = http.Request.Headers[HeaderName].ToString();
        if (string.IsNullOrWhiteSpace(key) || key.Length > MaxKeyLength)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Idempotency key required",
                detail: $"Send a unique '{HeaderName}' header (max {MaxKeyLength} characters) with this request.",
                extensions: new Dictionary<string, object?> { ["code"] = "Idempotency.KeyMissing" });
        }

        var store = http.RequestServices.GetRequiredService<IIdempotencyStore>();
        var user = http.RequestServices.GetRequiredService<ICurrentUser>();
        var route = (http.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? http.Request.Path.Value;
        var scopedKey = $"{http.Request.Method} {route} {user.Id}:{key}";
        var requestHash = HashArguments(context, http);
        var ct = http.RequestAborted;

        var stored = await store.GetAsync(scopedKey, ct).ConfigureAwait(false);
        if (stored is not null)
        {
            if (!string.Equals(stored.RequestHash, requestHash, StringComparison.Ordinal))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status422UnprocessableEntity,
                    title: "Idempotency key reused",
                    detail: "This idempotency key was already used with a different request.",
                    extensions: new Dictionary<string, object?> { ["code"] = "Idempotency.KeyReused" });
            }

            return new StoredResponseResult(stored);
        }

        var result = await next(context).ConfigureAwait(false);
        if (result is not IResult httpResult)
        {
            return result;
        }

        // Execute the result into a buffer so the exact bytes can be stored and replayed.
        var originalBody = http.Response.Body;
        await using var buffer = new MemoryStream();
        http.Response.Body = buffer;
        try
        {
            await httpResult.ExecuteAsync(http).ConfigureAwait(false);
        }
        finally
        {
            http.Response.Body = originalBody;
        }

        var body = buffer.ToArray();
        if (http.Response.StatusCode < 500)
        {
            await store.SaveAsync(scopedKey, new IdempotentResponse(requestHash, http.Response.StatusCode, http.Response.ContentType, body), ct)
                .ConfigureAwait(false);
        }

        await originalBody.WriteAsync(body, ct).ConfigureAwait(false);
        return Results.Empty;
    }

    private static string HashArguments(EndpointFilterInvocationContext context, HttpContext http)
    {
        var payload = context.Arguments
            .Where(a => a is not null and not HttpContext and not CancellationToken && !IsService(a))
            .ToList();
        var json = JsonSerializer.SerializeToUtf8Bytes(new { route = http.Request.RouteValues, payload });
        return Convert.ToHexString(SHA256.HashData(json));
    }

    private static bool IsService(object? argument) =>
        argument?.GetType().Namespace?.StartsWith("Microsoft.", StringComparison.Ordinal) == true
        || argument is IServiceProvider;

    private sealed class StoredResponseResult(IdempotentResponse stored) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.StatusCode = stored.StatusCode;
            if (stored.ContentType is not null)
            {
                httpContext.Response.ContentType = stored.ContentType;
            }

            httpContext.Response.Headers["Idempotent-Replayed"] = "true";
            await httpContext.Response.Body.WriteAsync(stored.Body, httpContext.RequestAborted).ConfigureAwait(false);
        }
    }
}

public static class IdempotencyEndpointExtensions
{
    /// <summary>Requires an <c>Idempotency-Key</c> header and replays stored responses (money-moving endpoints).</summary>
    public static RouteHandlerBuilder RequireIdempotencyKey(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter<IdempotencyFilter>();
}
