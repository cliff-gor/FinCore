using FinCore.Modules.Organization.Domain.Institutions;

namespace FinCore.Modules.Organization.Application.Abstractions;

public interface IInstitutionRepository
{
    Task<bool> CodeExistsAsync(string normalizedCode, CancellationToken cancellationToken);

    void Add(Institution institution);
}

public interface ICurrencyCatalog
{
    Task<bool> IsActiveAsync(string isoCode, CancellationToken cancellationToken);
}

/// <summary>Commits the module's changes (and its outbox messages) in one transaction.</summary>
public interface IOrganizationUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
