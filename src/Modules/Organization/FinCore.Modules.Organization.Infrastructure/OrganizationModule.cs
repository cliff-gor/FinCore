using FinCore.BuildingBlocks.Application;
using FinCore.BuildingBlocks.Infrastructure;
using FinCore.BuildingBlocks.Infrastructure.Persistence;
using FinCore.BuildingBlocks.Web;
using FinCore.Modules.Organization.Application.Abstractions;
using FinCore.Modules.Organization.Application.IntegrationEventHandlers;
using FinCore.Modules.Organization.Contracts;
using FinCore.Modules.Organization.Contracts.IntegrationEvents;
using FinCore.Modules.Organization.Infrastructure.ContractsImpl;
using FinCore.Modules.Organization.Infrastructure.Outbox;
using FinCore.Modules.Organization.Infrastructure.Persistence;
using FinCore.Modules.Organization.Infrastructure.Persistence.Repositories;
using FinCore.Modules.Organization.Presentation.Branches;
using FinCore.Modules.Organization.Presentation.Calendars;
using FinCore.Modules.Organization.Presentation.Institutions;
using FinCore.SharedKernel.Time;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinCore.Modules.Organization.Infrastructure;

/// <summary>Organization module: institutions, currencies and business date (spec §12).</summary>
public sealed class OrganizationModule : IModule
{
    public const string ModuleName = "Organization";

    public string Name => ModuleName;

    public IServiceCollection AddModule(IServiceCollection services, IConfiguration configuration)
    {
        var enabled = ModuleSwitch.IsEnabled(configuration, ModuleName);

        // The DbContext is registered even when the module is switched off: migrations must still run so the
        // schema is ready the day the module is enabled (spec §33.7.4).

        services.AddModuleDbContext<OrganizationDbContext>(
            configuration, ModuleName, OrganizationDbContext.SchemaName, migrationOrder: 1, enabled);

        services.AddHandlersFromAssembly(Application.AssemblyReference.Assembly);
        services.AddScoped<IInstitutionRepository, InstitutionRepository>();
        services.AddScoped<IBranchRepository, BranchRepository>();
        services.AddScoped<IHolidayCalendarRepository, HolidayCalendarRepository>();
        services.AddScoped<ICurrencyCatalog, CurrencyCatalog>();
        services.AddScoped<IOrganizationUnitOfWork, OrganizationUnitOfWork>();
        services.AddScoped<IOrganizationQueries, OrganizationQueries>();
        // One implementation serves both the module's public API and the platform-wide business date provider.
        services.AddScoped<OrganizationModuleApi>();
        services.AddScoped<IOrganizationModuleApi>(sp => sp.GetRequiredService<OrganizationModuleApi>());
        services.AddScoped<IBusinessDateProvider>(sp => sp.GetRequiredService<OrganizationModuleApi>());
        services.AddScoped<IHolidayCalendar>(sp => sp.GetRequiredService<OrganizationModuleApi>());
        services.AddDomainEventMapper<InstitutionCreatedMapper>();
        services.AddDomainEventMapper<InstitutionStatusChangedMapper>();
        services.AddDomainEventMapper<BranchCreatedMapper>();
        services.AddDomainEventMapper<BranchClosedMapper>();

        // Subscriptions, like endpoints and jobs, exist only while the module is enabled.
        services.AddScoped<IDevSeeder, DevSeeding.OrganizationDevSeeder>();

        if (enabled)
        {
            services.AddIntegrationEventHandler<InstitutionCreatedIntegrationEvent, InstitutionCreatedLoggingHandler, OrganizationDbContext>();
        }

        return services;
    }

    public IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder app) =>
        app.MapInstitutionEndpoints()
            .MapBranchEndpoints()
            .MapHolidayCalendarEndpoints();
}
