using FinCore.BuildingBlocks.Application.Audit;
using FinCore.BuildingBlocks.Application.EventBus;
using FinCore.BuildingBlocks.Application.Idempotency;
using FinCore.BuildingBlocks.Application.Security;
using FinCore.BuildingBlocks.Infrastructure.Audit;
using FinCore.BuildingBlocks.Infrastructure.EventBus;
using FinCore.BuildingBlocks.Infrastructure.Idempotency;
using FinCore.BuildingBlocks.Infrastructure.Inbox;
using FinCore.BuildingBlocks.Infrastructure.Outbox;
using FinCore.BuildingBlocks.Infrastructure.Persistence;
using FinCore.BuildingBlocks.Infrastructure.Persistence.Interceptors;
using FinCore.BuildingBlocks.Infrastructure.Security;
using FinCore.SharedKernel.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quartz;

namespace FinCore.BuildingBlocks.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Name of the single PostgreSQL connection string (one database, schema per module).</summary>
    public const string ConnectionStringName = "fincore";

    /// <summary>Shared infrastructure: time, event bus, outbox options/metrics, Quartz, platform DB + idempotency.</summary>
    public static IServiceCollection AddInfrastructureBuildingBlocks(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<ICurrentUser, SystemCurrentUser>();
        services.TryAddSingleton<IEventBus, InMemoryEventBus>();
        services.TryAddSingleton<IDomainEventMapperRegistry, DomainEventMapperRegistry>();
        services.TryAddScoped<AuditableEntityInterceptor>();
        services.TryAddScoped<DomainEventsToOutboxInterceptor>();
        services.TryAddSingleton<OutboxMetrics>();
        services.AddMetrics();
        services.AddOptions<OutboxOptions>().Bind(configuration.GetSection(OutboxOptions.SectionName));

        // RAM job store for M0: jobs are recreated at start-up. EOD (M5) moves Quartz to the clustered Postgres store so
        // two API tasks never run the same job twice (spec §33.5).
        services.AddQuartz();
        // Let in-flight jobs (e.g. an outbox batch) finish on shutdown instead of being cut off mid-publish.
        services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);

        services.AddPlatformDatabase(configuration);
        services.TryAddScoped<IIdempotencyStore, PostgresIdempotencyStore>();
        services.TryAddScoped<IAuditLog, PostgresAuditLog>();
        services.TryAddScoped<IRequestContext, NoRequestContext>();
        services.AddQuartz(q =>
        {
            var key = new JobKey("platform-purge-idempotency");
            q.AddJob<PurgeExpiredIdempotencyRecordsJob>(j => j.WithIdentity(key));
            q.AddTrigger(t => t.ForJob(key).WithSimpleSchedule(s => s.WithInterval(TimeSpan.FromHours(1)).RepeatForever()));
        });

        return services;
    }

    /// <summary>
    /// Registers only the platform database (idempotency, audit, BFF sessions, Data Protection keys). The gateway uses
    /// this on its own: it needs sessions and keys but none of the API's jobs, outbox or event bus.
    /// </summary>
    public static IServiceCollection AddPlatformDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddDbContext<PlatformDbContext>(options =>
            UsePostgres(options, configuration, PlatformDbContext.Schema));
        services.AddSingleton(new ModuleDatabase("Platform", typeof(PlatformDbContext), MigrationOrder: 0));
        return services;
    }

    /// <summary>
    /// Registers a module DbContext with its schema, migrations history table and interceptors. Migrations always
    /// register; the outbox job and backlog health check only when the module is <paramref name="enabled"/>.
    /// </summary>
    public static IServiceCollection AddModuleDbContext<TDbContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        string moduleName,
        string schema,
        int migrationOrder,
        bool enabled)
        where TDbContext : ModuleDbContext
    {
        services.AddDbContext<TDbContext>((sp, options) =>
        {
            UsePostgres(options, configuration, schema);
            options.AddInterceptors(
                sp.GetRequiredService<AuditableEntityInterceptor>(),
                sp.GetRequiredService<DomainEventsToOutboxInterceptor>());
        });
        services.AddSingleton(new ModuleDatabase(moduleName, typeof(TDbContext), migrationOrder));

        if (enabled)
        {
            services.AddHealthChecks().AddCheck<OutboxBacklogHealthCheck<TDbContext>>($"outbox-{schema}", tags: ["ready"]);
            services.AddScoped<ProcessOutboxJob<TDbContext>>();
            services.AddQuartz(q =>
            {
                var key = new JobKey($"{schema}-process-outbox");
                q.AddJob<ProcessOutboxJob<TDbContext>>(j => j.WithIdentity(key));
                q.AddTrigger(t => t.ForJob(key).StartNow().WithSimpleSchedule(s => s
                    .WithInterval(GetPollingInterval(configuration))
                    .RepeatForever()));
            });
        }

        return services;
    }

    /// <summary>Registers a subscriber wrapped in the inbox decorator of the subscribing module's DbContext.</summary>
    public static IServiceCollection AddIntegrationEventHandler<TEvent, THandler, TDbContext>(this IServiceCollection services)
        where TEvent : IIntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent>
        where TDbContext : ModuleDbContext
    {
        services.AddScoped<THandler>();
        services.AddScoped<IIntegrationEventHandler<TEvent>>(sp =>
            new IdempotentEventHandlerDecorator<TEvent, TDbContext>(
                sp.GetRequiredService<THandler>(),
                sp.GetRequiredService<TDbContext>(),
                sp.GetRequiredService<TimeProvider>()));
        return services;
    }

    public static IServiceCollection AddDomainEventMapper<TMapper>(this IServiceCollection services)
        where TMapper : class, IDomainEventMapper
    {
        services.AddSingleton<IDomainEventMapper, TMapper>();
        return services;
    }

    private static TimeSpan GetPollingInterval(IConfiguration configuration) =>
        configuration.GetSection(OutboxOptions.SectionName).Get<OutboxOptions>()?.PollingInterval
        ?? new OutboxOptions().PollingInterval;

    private static void UsePostgres(DbContextOptionsBuilder options, IConfiguration configuration, string schema)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");

        options
            // Each module keeps its migrations history in its own schema, so modules migrate independently (spec §11).
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(ModuleDbContext.MigrationsHistoryTable, schema))
            .UseSnakeCaseNamingConvention();
    }
}
