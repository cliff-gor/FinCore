using FinCore.BuildingBlocks.Application.Security;
using FinCore.BuildingBlocks.Web.Authorization;
using FinCore.BuildingBlocks.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FinCore.BuildingBlocks.Web;

public static class DependencyInjection
{
    public static IServiceCollection AddWebBuildingBlocks(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        // Replace, not add: infrastructure registers a system-user fallback for hosts without HTTP (e.g. the Migrator).
        services.Replace(ServiceDescriptor.Scoped<ICurrentUser, HttpCurrentUser>());
        services.AddScoped<IRequestContext, HttpRequestContext>();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        return services;
    }
}
