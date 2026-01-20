using System.Reflection;
using FinCore.BuildingBlocks.Application.Behaviors;
using FinCore.BuildingBlocks.Application.Messaging;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace FinCore.BuildingBlocks.Application;

public static class DependencyInjection
{
    /// <summary>Registers the dispatcher and the standard pipeline (logging → validation → handler).</summary>
    public static IServiceCollection AddApplicationBuildingBlocks(this IServiceCollection services)
    {
        services.AddScoped<IDispatcher, Dispatcher>();
        // Order matters: logging wraps validation, so rejected requests are logged with their failure code too.
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        return services;
    }

    /// <summary>Registers every request handler and validator found in a module's Application assembly.</summary>
    public static IServiceCollection AddHandlersFromAssembly(this IServiceCollection services, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var handlerTypes = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false });

        foreach (var type in handlerTypes)
        {
            foreach (var iface in type.GetInterfaces()
                         .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)))
            {
                services.AddScoped(iface, type);
            }
        }

        // Validators are internal sealed by convention (spec §34), so internal types must be included explicitly.
        services.AddValidatorsFromAssembly(assembly, ServiceLifetime.Scoped, includeInternalTypes: true);
        return services;
    }
}
