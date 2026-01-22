using FinCore.Modules.Organization.Domain.Branches;
using FinCore.Modules.Organization.Domain.Calendars;
using FinCore.Modules.Organization.Domain.Institutions;

namespace FinCore.Modules.Organization.Application.Abstractions;

/// <summary>Write-side access to institutions. Queries go through <see cref="IOrganizationQueries"/> instead.</summary>
public interface IInstitutionRepository
{
    Task<bool> CodeExistsAsync(string normalizedCode, CancellationToken cancellationToken);

    Task<Institution?> GetAsync(InstitutionId id, CancellationToken cancellationToken);

    void Add(Institution institution);
}

/// <summary>Write-side access to branches.</summary>
public interface IBranchRepository
{
    Task<Branch?> GetAsync(BranchId id, CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(InstitutionId institutionId, string code, CancellationToken cancellationToken);

    Task<bool> HasActiveBranchesAsync(InstitutionId institutionId, CancellationToken cancellationToken);

    /// <summary>Whether any branch (active or closed) names this one as parent; such a branch cannot get a parent itself.</summary>
    Task<bool> HasChildrenAsync(BranchId id, CancellationToken cancellationToken);

    Task<bool> HasActiveChildrenAsync(BranchId id, CancellationToken cancellationToken);

    void Add(Branch branch);
}

/// <summary>Write-side access to holiday calendars (one per institution).</summary>
public interface IHolidayCalendarRepository
{
    Task<HolidayCalendar?> GetAsync(InstitutionId institutionId, CancellationToken cancellationToken);

    void Add(HolidayCalendar calendar);
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
