using FinCore.BuildingBlocks.Infrastructure.Persistence;
using FinCore.Modules.Identity.Domain.Approvals;
using FinCore.Modules.Identity.Domain.Institutions;
using FinCore.Modules.Identity.Domain.Staff;
using Microsoft.EntityFrameworkCore;

namespace FinCore.Modules.Identity.Infrastructure.Persistence;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "identity";

    public override string Schema => SchemaName;

    public DbSet<StaffUser> StaffUsers => Set<StaffUser>();

    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();

    public DbSet<InstitutionSnapshot> Institutions => Set<InstitutionSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
