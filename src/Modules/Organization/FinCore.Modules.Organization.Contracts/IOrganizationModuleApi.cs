using FinCore.Modules.Organization.Contracts.Dtos;

namespace FinCore.Modules.Organization.Contracts;

/// <summary>Read-only API other modules use to query Organization (spec §10). Never returns domain entities.</summary>
public interface IOrganizationModuleApi
{
    Task<InstitutionSummaryDto?> GetInstitutionAsync(Guid institutionId, CancellationToken cancellationToken);

    Task<DateOnly?> GetBusinessDateAsync(Guid institutionId, CancellationToken cancellationToken);

    /// <summary>Looks an institution up by its immutable code (e.g. <c>FCB</c>).</summary>
    Task<InstitutionSummaryDto?> FindInstitutionByCodeAsync(string code, CancellationToken cancellationToken);

    /// <summary>Looks a branch up by its institution and three-digit code.</summary>
    Task<BranchSummaryDto?> FindBranchByCodeAsync(Guid institutionId, string code, CancellationToken cancellationToken);

    /// <summary>A branch of the given institution, or null if it does not exist there (never another institution's branch).</summary>
    Task<BranchSummaryDto?> GetBranchAsync(Guid institutionId, Guid branchId, CancellationToken cancellationToken);
}

/// <summary>
/// Working-day rules per institution (spec 002): weekends and public holidays. Loans use it to shift due dates and
/// EOD to find the next business date, so every module asks Organization instead of hard-coding weekends.
/// </summary>
public interface IHolidayCalendar
{
    Task<bool> IsWorkingDayAsync(Guid institutionId, DateOnly date, CancellationToken cancellationToken);

    Task<DateOnly> NextWorkingDayAsync(Guid institutionId, DateOnly date, CancellationToken cancellationToken);
}
