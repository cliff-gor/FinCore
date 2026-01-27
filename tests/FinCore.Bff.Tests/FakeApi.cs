using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace FinCore.Bff.Tests;

/// <summary>
/// A real Kestrel server standing in for FinCore.Api (so YARP has something to proxy to) and for the identity
/// provider's back-channel endpoints (token refresh, revocation).
/// </summary>
public sealed class FakeApi : IAsyncLifetime
{
    /// <summary>Access token for which <c>/api/v1/me</c> answers 403 <c>Identity.StaffNotRegistered</c>.</summary>
    public const string UnregisteredAccessToken = "access-unregistered";

    private WebApplication? _app;

    public string Address { get; private set; } = string.Empty;

    /// <summary>Refresh tokens revoked through <c>/idp/revoke</c>.</summary>
    public ConcurrentBag<string> RevokedTokens { get; } = [];

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        _app = builder.Build();
        _app.MapGet("/api/v1/institutions", (HttpRequest r) => Results.Ok(new
        {
            proxied = true,
            csrf = r.Headers["X-CSRF"].ToString(),
            authorization = r.Headers.Authorization.ToString(),
            cookie = r.Headers.Cookie.ToString(),
        }));
        _app.MapGet("/api/v1/me", (HttpRequest r) => r.Headers.Authorization.ToString() switch
        {
            "Bearer " + UnregisteredAccessToken => Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Not registered",
                extensions: new Dictionary<string, object?> { ["code"] = "Identity.StaffNotRegistered" }),
            { Length: > 0 } => Results.Ok(new { id = "staff-1", kind = "staff", name = "Admin One", email = "admin1.fcb@fincore.local" }),
            _ => Results.Unauthorized(),
        });
        _app.MapPost("/idp/revoke", async (HttpRequest r) =>
        {
            var form = await r.ReadFormAsync();
            RevokedTokens.Add(form["token"].ToString());
            return Results.Ok();
        });
        _app.MapPost("/idp/token", () => Results.Json(new { access_token = "access-2", refresh_token = "refresh-2", expires_in = 300 }));
        _app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" }));
        _app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
        await _app.StartAsync();
        Address = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}
