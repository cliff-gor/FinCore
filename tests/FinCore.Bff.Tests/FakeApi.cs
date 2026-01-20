using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace FinCore.Bff.Tests;

/// <summary>A real Kestrel server standing in for FinCore.Api, so YARP has something to proxy to.</summary>
public sealed class FakeApi : IAsyncLifetime
{
    private WebApplication? _app;

    public string Address { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        _app = builder.Build();
        _app.MapGet("/api/v1/institutions", (HttpRequest r) => Results.Ok(new { proxied = true, csrf = r.Headers["X-CSRF"].ToString() }));
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
