using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinCore.BuildingBlocks.Web;

/// <summary>
/// A business module plugged into the host (spec §8). <see cref="AddModule"/> always runs (so migrations and
/// contracts exist); endpoints, jobs and subscriptions are only registered when the module is enabled.
/// </summary>
public interface IModule
{
    string Name { get; }

    IServiceCollection AddModule(IServiceCollection services, IConfiguration configuration);

    IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder app);
}

/// <summary>Reads <c>Modules:&lt;Name&gt;:Enabled</c> (spec §33.7.4). Modules are enabled unless switched off.</summary>
public static class ModuleSwitch
{
    public static bool IsEnabled(IConfiguration configuration, string moduleName)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return configuration.GetValue($"Modules:{moduleName}:Enabled", defaultValue: true);
    }
}
