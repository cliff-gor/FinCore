using FinCore.Modules.Organization.Domain.Institutions;

namespace FinCore.Modules.Organization.Application.Abstractions;

/// <summary>Write-side access to institutions. Queries go through <see cref="IOrganizationQueries"/> instead.</summary>
public interface IInstitutionRepository
{
    Task<bool> CodeExistsAsync(string normalizedCode, CancellationToken cancellationToken);

    void Add(Institution institution);
}

/// <summary>Reference data lookup for supported currencies.</summary>
public interface ICurrencyCatalog
{
    Task<bool> IsActiveAsync(string isoCode, CancellationToken cancellationToken);
}

/// <summary>Commits the module's changes (and its outbox messages) in one transaction.</summary>
public interface IOrganizationUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
