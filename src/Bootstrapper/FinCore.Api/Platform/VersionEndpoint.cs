using FinCore.BuildingBlocks.Infrastructure.Persistence;
using FinCore.BuildingBlocks.Web;
using Microsoft.EntityFrameworkCore;

namespace FinCore.Api.Platform;

public sealed record ModuleVersion(string Name, bool Enabled, string? Schema);

public sealed record VersionResponse(
    string Product,
    string Commit,
    string? BuiltAtUtc,
    string Environment,
    string ApiContract,
    string MinimumMobileVersion,
    IReadOnlyList<ModuleVersion> Modules);

/// <summary><c>GET /api/version</c> — anonymous; answers "what is running here?" (spec §33.7.3).</summary>
internal static class VersionEndpoint
{
    public const string DefaultMinimumMobileVersion = "0.1.0";

    public static WebApplication MapVersionEndpoint(this WebApplication app)
    {
        app.MapGet("/api/version", GetAsync)
            .AllowAnonymous()
            .ExcludeFromDescription();
        return app;
    }

    private static async Task<IResult> GetAsync(
        IServiceProvider services,
        IEnumerable<IModule> modules,
        IEnumerable<ModuleDatabase> databases,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var databaseByModule = databases.ToDictionary(d => d.ModuleName, StringComparer.Ordinal);
        var result = new List<ModuleVersion>();

        foreach (var module in modules.OrderBy(m => m.Name, StringComparer.Ordinal))
        {
            string? schema = null;
            if (databaseByModule.TryGetValue(module.Name, out var database))
            {
                try
                {
                    var db = (DbContext)services.GetRequiredService(database.DbContextType);
                    schema = (await db.Database.GetAppliedMigrationsAsync(ct).ConfigureAwait(false)).LastOrDefault();
                }
#pragma warning disable CA1031 // The version endpoint must answer even when the database is down.
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    loggerFactory.CreateLogger(typeof(VersionEndpoint)).LogWarning(ex, "Could not read migrations for {Module}", module.Name);
                }
            }

            result.Add(new ModuleVersion(module.Name, ModuleSwitch.IsEnabled(configuration, module.Name), schema));
        }

        return TypedResults.Ok(new VersionResponse(
            BuildInfo.Product,
            BuildInfo.Commit,
            BuildInfo.BuiltAtUtc,
            environment.EnvironmentName.ToLowerInvariant(),
            "v1",
            configuration["Mobile:MinimumVersion"] ?? DefaultMinimumMobileVersion,
            result));
    }
}
