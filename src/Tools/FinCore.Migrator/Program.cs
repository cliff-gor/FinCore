using FinCore.BuildingBlocks.Application;
using FinCore.BuildingBlocks.Infrastructure;
using FinCore.BuildingBlocks.Infrastructure.Persistence;
using FinCore.BuildingBlocks.Web;
using FinCore.Modules.Organization.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// FinCore.Migrator — applies every module's EF Core migrations in a fixed order (spec §33.10).
// Runs as a one-off task with a DDL-capable role before each deployment; the API never migrates outside Development.
var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddApplicationBuildingBlocks()
    .AddInfrastructureBuildingBlocks(builder.Configuration);

// TODO(M1): list each new module here as it lands; the order in which modules migrate comes from their MigrationOrder.
IModule[] modules = [new OrganizationModule()];
foreach (var module in modules)
{
    module.AddModule(builder.Services, builder.Configuration);
}

// Build the container without starting hosted services (no jobs run in the migrator).
using var host = builder.Build();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("FinCore.Migrator");

try
{
    await using var scope = host.Services.CreateAsyncScope();
    foreach (var database in scope.ServiceProvider.GetServices<ModuleDatabase>().OrderBy(d => d.MigrationOrder))
    {
        var db = (DbContext)scope.ServiceProvider.GetRequiredService(database.DbContextType);
        var pending = (await db.Database.GetPendingMigrationsAsync().ConfigureAwait(false)).ToList();
        logger.LogInformation("{Module}: {Count} pending migration(s)", database.ModuleName, pending.Count);
        await db.Database.MigrateAsync().ConfigureAwait(false);
    }

    logger.LogInformation("All module databases are up to date");
    return 0;
}
#pragma warning disable CA1031 // Top-level: report and fail the task with a non-zero exit code.
catch (Exception ex)
#pragma warning restore CA1031
{
    logger.LogCritical(ex, "Migration failed");
    return 1;
}
