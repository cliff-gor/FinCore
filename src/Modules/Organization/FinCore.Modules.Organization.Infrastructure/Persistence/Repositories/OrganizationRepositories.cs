using FinCore.Modules.Organization.Application.Abstractions;
using FinCore.Modules.Organization.Domain.Branches;
using FinCore.Modules.Organization.Domain.Calendars;
using FinCore.Modules.Organization.Domain.Institutions;
using Microsoft.EntityFrameworkCore;

namespace FinCore.Modules.Organization.Infrastructure.Persistence.Repositories;

internal sealed class InstitutionRepository(OrganizationDbContext dbContext) : IInstitutionRepository
{
    // Compares through the value converter, so the query hits the unique index on code.
    public Task<bool> CodeExistsAsync(string normalizedCode, CancellationToken cancellationToken) =>
        dbContext.Institutions.AnyAsync(i => i.Code == InstitutionCode.FromStorage(normalizedCode), cancellationToken);

    public Task<Institution?> GetAsync(InstitutionId id, CancellationToken cancellationToken) =>
        dbContext.Institutions.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public void Add(Institution institution) => dbContext.Institutions.Add(institution);
}

internal sealed class BranchRepository(OrganizationDbContext dbContext) : IBranchRepository
{
    public Task<Branch?> GetAsync(BranchId id, CancellationToken cancellationToken) =>
        dbContext.Branches.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    public Task<bool> CodeExistsAsync(InstitutionId institutionId, string code, CancellationToken cancellationToken) =>
        dbContext.Branches.AnyAsync(b => b.InstitutionId == institutionId && b.Code == BranchCode.FromStorage(code), cancellationToken);

    public Task<bool> HasActiveBranchesAsync(InstitutionId institutionId, CancellationToken cancellationToken) =>
        dbContext.Branches.AnyAsync(b => b.InstitutionId == institutionId && b.Status == BranchStatus.Active, cancellationToken);

    public Task<bool> HasChildrenAsync(BranchId id, CancellationToken cancellationToken) =>
        dbContext.Branches.AnyAsync(b => b.ParentBranchId == id, cancellationToken);

    public Task<bool> HasActiveChildrenAsync(BranchId id, CancellationToken cancellationToken) =>
        dbContext.Branches.AnyAsync(b => b.ParentBranchId == id && b.Status == BranchStatus.Active, cancellationToken);

    public void Add(Branch branch) => dbContext.Branches.Add(branch);
}

internal sealed class HolidayCalendarRepository(OrganizationDbContext dbContext) : IHolidayCalendarRepository
{
    public Task<HolidayCalendar?> GetAsync(InstitutionId institutionId, CancellationToken cancellationToken) =>
        dbContext.HolidayCalendars.FirstOrDefaultAsync(c => c.Id == institutionId, cancellationToken);

    public void Add(HolidayCalendar calendar) => dbContext.HolidayCalendars.Add(calendar);
}

internal sealed class CurrencyCatalog(OrganizationDbContext dbContext) : ICurrencyCatalog
{
    public Task<bool> IsActiveAsync(string isoCode, CancellationToken cancellationToken) =>
        dbContext.Currencies.AnyAsync(c => c.IsoCode == isoCode && c.IsActive, cancellationToken);
}

internal sealed class OrganizationUnitOfWork(OrganizationDbContext dbContext) : IOrganizationUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
