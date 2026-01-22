using FinCore.BuildingBlocks.Infrastructure.Persistence;
using FinCore.Modules.Organization.Domain.Branches;
using FinCore.Modules.Organization.Domain.Calendars;
using FinCore.Modules.Organization.Domain.Currencies;
using FinCore.Modules.Organization.Domain.Institutions;
using Microsoft.EntityFrameworkCore;

namespace FinCore.Modules.Organization.Infrastructure.Persistence;

public sealed class OrganizationDbContext(DbContextOptions<OrganizationDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "org";

    public override string Schema => SchemaName;

    public DbSet<Institution> Institutions => Set<Institution>();

    public DbSet<SupportedCurrency> Currencies => Set<SupportedCurrency>();

    public DbSet<Branch> Branches => Set<Branch>();

    public DbSet<HolidayCalendar> HolidayCalendars => Set<HolidayCalendar>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrganizationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
