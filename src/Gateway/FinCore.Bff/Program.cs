using FinCore.Bff;
using FinCore.Bff.Security;

var builder = WebApplication.CreateBuilder(args);

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
