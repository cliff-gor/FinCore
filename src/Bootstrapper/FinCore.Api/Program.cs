using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Serialization;
using FinCore.Api;
using FinCore.Api.DevSeeding;
using FinCore.Api.Middleware;
using FinCore.Api.Platform;
using FinCore.BuildingBlocks.Application;
using FinCore.BuildingBlocks.Infrastructure;
using FinCore.BuildingBlocks.Web;
using FinCore.Modules.Identity.Infrastructure;
using FinCore.Modules.Organization.Infrastructure;
using FinCore.BuildingBlocks.Web.Authorization;
using Scalar.AspNetCore;
using Serilog;

// FinCore.Api — the single host and composition root of the modular monolith (spec §6, §8).
// It wires cross-cutting concerns and registers modules; it contains no business logic of its own.
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddSerilog(
    (services, logger) =>
    {
        logger.ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "FinCore.Api")
            .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture);

        var seqUrl = builder.Configuration["Seq:ServerUrl"];
        if (!string.IsNullOrWhiteSpace(seqUrl))
        {
            logger.WriteTo.Seq(seqUrl, formatProvider: CultureInfo.InvariantCulture);
        }
    },
    // Keep other logging providers (OpenTelemetry) receiving logs too, so the Aspire dashboard still shows them.
    writeToProviders: true);

// Enums travel as strings on the wire (spec §28): "Bank" is self-describing, 1 is not.
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// Every error response carries a traceId, so a support ticket can be matched to logs and traces.
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions.TryAdd("traceId", Activity.Current?.Id ?? ctx.HttpContext.TraceIdentifier));
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services
    .AddApplicationBuildingBlocks()
    .AddInfrastructureBuildingBlocks(builder.Configuration)
    .AddWebBuildingBlocks()
    .AddModules(builder.Configuration, typeof(OrganizationModule).Assembly, typeof(IdentityModule).Assembly);

builder.Services.AddHostedService<DatabaseInitializer>();

builder.Services.AddHealthChecks()
    .AddNpgSql(
        sp => sp.GetRequiredService<IConfiguration>().GetConnectionString(FinCore.BuildingBlocks.Infrastructure.DependencyInjection.ConnectionStringName)!,
        name: "postgres",
        tags: ["ready"]);

// Staff and customer tokens come from two identity pools; permissions are added by the Identity module (spec 002).
builder.Services.AddFinCoreAuthentication(builder.Configuration);

// One OpenAPI document per module (spec §28); the build exports each to /openapi for the front-end client.
foreach (var (document, title) in new[] { ("organization", "FinCore Organization API"), ("identity", "FinCore Identity API") })
{
    builder.Services.AddOpenApi(document, o => o.AddDocumentTransformer((doc, _, _) =>
    {
        doc.Info.Title = title;
        doc.Info.Version = BuildInfo.Product;
        return Task.CompletedTask;
    }));
}

var app = builder.Build();

// Middleware order matters: exceptions are caught first, correlation and version headers are set before any
// response starts, and authentication must run before authorization.
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<VersionHeaderMiddleware>();
app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();

// API docs are served only in Development; other environments use the committed snapshot in /openapi.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi("/openapi/{documentName}.json");
    app.MapScalarApiReference();
}

app.MapModules();
app.MapVersionEndpoint();
app.MapDefaultEndpoints();

await app.RunAsync().ConfigureAwait(false);

/// <summary>Entry point; public for WebApplicationFactory-based tests.</summary>
public partial class Program;
