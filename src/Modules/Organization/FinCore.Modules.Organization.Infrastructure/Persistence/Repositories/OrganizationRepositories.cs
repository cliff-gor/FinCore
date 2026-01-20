using FinCore.Modules.Organization.Application.Abstractions;
using FinCore.Modules.Organization.Domain.Institutions;
using Microsoft.EntityFrameworkCore;

namespace FinCore.Modules.Organization.Infrastructure.Persistence.Repositories;

internal sealed class InstitutionRepository(OrganizationDbContext dbContext) : IInstitutionRepository
{
    public Task<bool> CodeExistsAsync(string normalizedCode, CancellationToken cancellationToken) =>
        dbContext.Institutions.AnyAsync(i => i.Code == InstitutionCode.FromStorage(normalizedCode), cancellationToken);

    public void Add(Institution institution) => dbContext.Institutions.Add(institution);
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
