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
using FinCore.Modules.Organization.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Scalar.AspNetCore;
using Serilog;

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
    writeToProviders: true);

builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions.TryAdd("traceId", Activity.Current?.Id ?? ctx.HttpContext.TraceIdentifier));
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services
    .AddApplicationBuildingBlocks()
    .AddInfrastructureBuildingBlocks(builder.Configuration)
    .AddWebBuildingBlocks()
    .AddModules(builder.Configuration, typeof(OrganizationModule).Assembly);

builder.Services.AddScoped<DevSeeder>();
builder.Services.AddHostedService<DatabaseInitializer>();

builder.Services.AddHealthChecks()
    .AddNpgSql(
        sp => sp.GetRequiredService<IConfiguration>().GetConnectionString(FinCore.BuildingBlocks.Infrastructure.DependencyInjection.ConnectionStringName)!,
        name: "postgres",
        tags: ["ready"]);

// Authentication is wired but permissions come from Identity (M1); outside Development, module endpoints fail closed.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.Authority = builder.Configuration["Auth:Authority"];
        o.Audience = builder.Configuration["Auth:Audience"];
        o.RequireHttpsMetadata = builder.Configuration.GetValue("Auth:RequireHttpsMetadata", true);
        o.MapInboundClaims = false;
    });
builder.Services.AddAuthorization();

builder.Services.AddOpenApi("organization", o => o.AddDocumentTransformer((document, _, _) =>
{
    document.Info.Title = "FinCore Organization API";
    document.Info.Version = BuildInfo.Product;
    return Task.CompletedTask;
}));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<VersionHeaderMiddleware>();
app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();

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
