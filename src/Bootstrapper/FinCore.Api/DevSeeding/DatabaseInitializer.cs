using FinCore.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinCore.Api.DevSeeding;

/// <summary>
/// Applies every module's migrations at startup and runs the dev seeder — Development only
/// (<c>Database:MigrateOnStartup</c>). Production migrates through FinCore.Migrator (spec §11, §33.10).
/// </summary>
internal sealed class DatabaseInitializer(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<DatabaseInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue<bool>("Database:MigrateOnStartup") || IsDesignTimeTool())
        {
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        // Same order as the Migrator, so local start-up and deployments create schemas identically.
        foreach (var database in scope.ServiceProvider.GetServices<ModuleDatabase>().OrderBy(d => d.MigrationOrder))
        {
            var db = (DbContext)scope.ServiceProvider.GetRequiredService(database.DbContextType);
            await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Migrated {Module} database", database.ModuleName);
        }

        if (configuration.GetValue<bool>("Database:SeedDevData"))
        {
            await scope.ServiceProvider.GetRequiredService<DevSeeder>().SeedAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Build-time OpenAPI generation and <c>dotnet ef</c> start the host but must never touch a database.</summary>
    private static bool IsDesignTimeTool() =>
        System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name is "GetDocument.Insider" or "ef";
}
