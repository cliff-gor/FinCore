using System.Reflection;
using FinCore.BuildingBlocks.Application.Security;
using FinCore.BuildingBlocks.Web;
using FinCore.BuildingBlocks.Web.Authorization;

namespace FinCore.Api;

/// <summary>Discovers <see cref="IModule"/> implementations and wires them into the host (spec §8).</summary>
internal static class ModuleRegistry
{
    public const string ApiPrefix = "/api/v1";

    public static IServiceCollection AddModules(this IServiceCollection services, IConfiguration configuration, params Assembly[] assemblies)
    {
        // Modules are discovered by reflection, so adding a module means adding one assembly to Program.cs and
        // nothing else in the host.
        var modules = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(IModule).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false })
            .Select(t => (IModule)Activator.CreateInstance(t)!)
            .ToList();

        foreach (var module in modules)
        {
            module.AddModule(services, configuration);
            services.AddSingleton(module);
        }

        services.AddSingleton<IEnabledModules>(new EnabledModules(
            modules.Where(m => ModuleSwitch.IsEnabled(configuration, m.Name)).Select(m => m.Name).Order(StringComparer.Ordinal).ToList()));
        return services;
    }

    /// <summary>
    /// Maps enabled modules under <c>/api/v1</c>. Every endpoint requires authentication in every environment
    /// (spec 002 removed M0's Development-only anonymous access), and the suspended-institution guard is applied once
    /// here so no module can forget it.
    /// </summary>
    public static WebApplication MapModules(this WebApplication app)
    {
        var group = app.MapGroup(ApiPrefix)
            .RequireAuthorization()
            .AddEndpointFilter<SuspendedInstitutionFilter>();

        foreach (var module in app.Services.GetServices<IModule>())
        {
            if (ModuleSwitch.IsEnabled(app.Configuration, module.Name))
            {
                module.MapEndpoints(group);
            }
        }

        return app;
    }
}

/// <summary>Module switches as read at start-up; /me and /api/version report them to the clients.</summary>
internal sealed record EnabledModules(IReadOnlyList<string> Names) : IEnabledModules;
