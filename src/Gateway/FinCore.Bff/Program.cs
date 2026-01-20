using FinCore.Bff;
using FinCore.Bff.Security;

var builder = WebApplication.CreateBuilder(args);

// One gateway binary, three behaviours. Reading the mode first lets each mode layer its own settings file on top.
var mode = GatewayModeConfiguration.Read(builder.Configuration);
builder.Configuration.AddJsonFile($"appsettings.{mode}.json", optional: false, reloadOnChange: true);
// Re-apply environment variables and command line so they still override the mode file.
builder.Configuration.AddEnvironmentVariables();
builder.Configuration.AddCommandLine(args);

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();
builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseExceptionHandler();
app.UseMiddleware<SecurityHeadersMiddleware>();

// Web modes protect cookie sessions with the CSRF header; the mobile mode uses bearer tokens instead, so the CSRF
// check would only get in the way there (spec §30.3, §31.4).
if (mode.IsWeb())
{
    app.UseMiddleware<CsrfHeaderMiddleware>();
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
