using FinCore.Bff;
using FinCore.Bff.Security;
using FinCore.Bff.Sessions;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

// One gateway binary, three behaviours. Reading the mode first lets each mode layer its own settings file on top.
var mode = GatewayModeConfiguration.Read(builder.Configuration);
builder.Configuration.AddJsonFile($"appsettings.{mode}.json", optional: false, reloadOnChange: true);
// Re-apply environment variables and command line so they still override the mode file.
builder.Configuration.AddEnvironmentVariables();
builder.Configuration.AddCommandLine(args);

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();

var proxy = builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
if (mode.IsWeb())
{
    builder.AddBffSessions(mode);

    proxy.AddTransforms(transforms =>
    {
        // The session cookie is the BFF's business only; the API (and its logs) never see the session id.
        transforms.AddRequestHeaderRemove("Cookie");
        if (string.Equals(transforms.Route.RouteId, DevSpaProxy.RouteId, StringComparison.Ordinal))
        {
            transforms.AddRequestTransform(ctx =>
            {
                ctx.HttpContext.Items[SecurityHeadersMiddleware.DevSpaResponseItem] = true;
                return ValueTask.CompletedTask;
            });
        }
    });

    if (builder.Environment.IsDevelopment() && builder.Configuration["Bff:DevSpaUrl"] is { Length: > 0 } devSpaUrl)
    {
        proxy.AddDevSpaRoute(devSpaUrl);
    }
}

var app = builder.Build();

app.UseExceptionHandler();
app.UseMiddleware<SecurityHeadersMiddleware>();

// Web modes protect cookie sessions with the CSRF header; the mobile mode uses bearer tokens instead, so the CSRF
// check would only get in the way there (spec §30.3, §31.4).
if (mode.IsWeb())
{
    // CSRF first: a forged request is refused before it costs a session lookup.
    app.UseMiddleware<CsrfHeaderMiddleware>();
    // Also handles the OIDC callback (/signin-oidc).
    app.UseAuthentication();
    app.UseRateLimiter();
    app.UseMiddleware<BffAccessTokenMiddleware>();
    app.MapBffEndpoints();
}
else
{
    app.UseMiddleware<MobileVersionMiddleware>();
}

// The gateway's own liveness (ALB target health); /health itself is proxied to the API in Mobile mode.
app.MapGet("/gateway/health", () => Results.Ok(new { status = "Healthy", mode = mode.ToString() }));
app.MapReverseProxy();

await app.RunAsync().ConfigureAwait(false);

/// <summary>Entry point; public for WebApplicationFactory-based tests.</summary>
public partial class Program;
