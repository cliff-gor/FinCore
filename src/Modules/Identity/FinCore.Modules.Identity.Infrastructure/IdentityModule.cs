using FinCore.BuildingBlocks.Application;
using FinCore.BuildingBlocks.Infrastructure;
using FinCore.BuildingBlocks.Web;
using FinCore.Modules.Identity.Application.Abstractions;
using FinCore.Modules.Identity.Application.Approvals;
using FinCore.Modules.Identity.Application.IntegrationEventHandlers;
using FinCore.Modules.Identity.Application.Staff;
using FinCore.Modules.Identity.Contracts;
using FinCore.Modules.Identity.Contracts.IntegrationEvents;
using FinCore.Modules.Identity.Infrastructure.ContractsImpl;
using FinCore.Modules.Identity.Infrastructure.Jobs;
using FinCore.Modules.Identity.Infrastructure.Outbox;
using FinCore.Modules.Identity.Infrastructure.Persistence;
using FinCore.Modules.Identity.Infrastructure.Persistence.Repositories;
using FinCore.Modules.Identity.Infrastructure.Security;
using FinCore.Modules.Identity.Presentation;
using FinCore.Modules.Organization.Contracts.IntegrationEvents;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace FinCore.Modules.Identity.Infrastructure;

/// <summary>Identity & Approvals module: staff, roles, permissions and maker-checker (master §13, spec 002).</summary>
public sealed class IdentityModule : IModule
{
    public const string ModuleName = "Identity";

    public string Name => ModuleName;

    public IServiceCollection AddModule(IServiceCollection services, IConfiguration configuration)
    {
        var enabled = ModuleSwitch.IsEnabled(configuration, ModuleName);

        services.AddModuleDbContext<IdentityDbContext>(
            configuration, ModuleName, IdentityDbContext.SchemaName, migrationOrder: 2, enabled);

        services.AddHandlersFromAssembly(Application.AssemblyReference.Assembly);
        services.AddOptions<ApprovalOptions>().Bind(configuration.GetSection(ApprovalOptions.SectionName));
        services.AddScoped<ApprovalOperationCatalog>();
        services.AddScoped<ApprovalPolicy>();
        services.AddScoped<IdentityOperationExecutor>();
        services.AddScoped<ApprovalDecisionService>();
        services.AddScoped<ManagedStaffLoader>();
        services.AddScoped<ImmediateAccessChange>();

        services.AddScoped<IStaffRepository, StaffRepository>();
        services.AddScoped<IApprovalRepository, ApprovalRepository>();
        services.AddScoped<IIdentityQueries, IdentityQueries>();
        services.AddScoped<IInstitutionDirectory, InstitutionDirectory>();
        services.AddScoped<IIdentityUnitOfWork, IdentityUnitOfWork>();
        services.AddScoped<IIdentityModuleApi, IdentityModuleApi>();

        // Permissions come from Identity on every request, so the transformation (and its cache) is registered
        // whether or not the module's own endpoints are enabled: every other module depends on it.
        services.AddHybridCache();
        services.AddScoped<StaffAccessService>();
        services.AddScoped<IAccessCache>(sp => sp.GetRequiredService<StaffAccessService>());
        services.AddScoped<IClaimsTransformation, FinCoreClaimsTransformation>();

        services.AddScoped<FinCore.BuildingBlocks.Infrastructure.Persistence.IDevSeeder, DevSeeding.IdentityDevSeeder>();
        services.AddDomainEventMapper<StaffAccessChangedMapper>();
        services.AddDomainEventMapper<ApprovalRequestDecidedMapper>();

        if (enabled)
        {
            services.AddIntegrationEventHandler<InstitutionCreatedIntegrationEvent, InstitutionCreatedHandler, IdentityDbContext>();
            services.AddIntegrationEventHandler<InstitutionStatusChangedIntegrationEvent, InstitutionStatusChangedHandler, IdentityDbContext>();
            services.AddIntegrationEventHandler<StaffAccessChangedIntegrationEvent, StaffAccessChangedHandler, IdentityDbContext>();
            services.AddIntegrationEventHandler<ApprovalRequestedIntegrationEvent, ApprovalRequestedHandler, IdentityDbContext>();

            services.AddScoped<ExpireApprovalRequestsJob>();
            services.AddQuartz(q =>
            {
                var key = new JobKey("identity-expire-approvals");
                q.AddJob<ExpireApprovalRequestsJob>(j => j.WithIdentity(key));
                // Once a day at 01:00 UTC; expiry is measured in days, so finer scheduling would add nothing.
                q.AddTrigger(t => t.ForJob(key).WithCronSchedule("0 0 1 * * ?"));
            });
        }

        return services;
    }

    public IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder app) => app.MapIdentityEndpoints();
}
