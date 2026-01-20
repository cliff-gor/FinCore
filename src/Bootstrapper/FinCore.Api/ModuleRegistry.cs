using System.Reflection;
using FinCore.BuildingBlocks.Web;

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

        return services;
    }

    /// <summary>
    /// Maps enabled modules under <c>/api/v1</c>. In Development only, module endpoints allow anonymous access
    /// until the Identity module (M1) supplies permissions; every other environment fails closed (spec 001).
    /// </summary>
    public static WebApplication MapModules(this WebApplication app)
    {
        var group = app.MapGroup(ApiPrefix);
        if (app.Environment.IsDevelopment())
        {
            group.AllowAnonymous();
        }

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
