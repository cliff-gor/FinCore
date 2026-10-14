using FinCore.Modules.Organization.Application.Abstractions;
using FinCore.Modules.Organization.Contracts;
using FinCore.Modules.Organization.Contracts.Dtos;
using FinCore.Modules.Organization.Domain.Calendars;
using FinCore.Modules.Organization.Domain.Institutions;
using FinCore.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace FinCore.Modules.Organization.Infrastructure.ContractsImpl;

/// <summary>
/// In-process implementation of the module's public API. If Organization is ever extracted (spec §36), other modules
/// keep the same interfaces and get an HTTP client behind them instead.
/// </summary>
internal sealed class OrganizationModuleApi(
    IOrganizationQueries queries,
    IHolidayCalendarRepository calendars,
    Persistence.OrganizationDbContext dbContext)
    : IOrganizationModuleApi, IBusinessDateProvider, IHolidayCalendar
{
    public async Task<InstitutionSummaryDto?> FindInstitutionByCodeAsync(string code, CancellationToken cancellationToken)
    {
        var normalized = InstitutionCode.Normalize(code);
        var id = await dbContext.Institutions.AsNoTracking()
            .Where(i => i.Code == InstitutionCode.FromStorage(normalized))
            .Select(i => (Guid?)i.Id.Value)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return id is null ? null : await GetInstitutionAsync(id.Value, cancellationToken).ConfigureAwait(false);
    }

    public async Task<BranchSummaryDto?> FindBranchByCodeAsync(Guid institutionId, string code, CancellationToken cancellationToken)
    {
        var branchId = await dbContext.Branches.AsNoTracking()
            .Where(b => b.InstitutionId == new InstitutionId(institutionId) && b.Code == Domain.Branches.BranchCode.FromStorage(code))
            .Select(b => (Guid?)b.Id.Value)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return branchId is null ? null : await GetBranchAsync(institutionId, branchId.Value, cancellationToken).ConfigureAwait(false);
    }

    public async Task<InstitutionSummaryDto?> GetInstitutionAsync(Guid institutionId, CancellationToken cancellationToken)
    {
        var i = await queries.GetInstitutionAsync(institutionId, cancellationToken).ConfigureAwait(false);
        return i is null ? null : new InstitutionSummaryDto(i.Id, i.Code, i.Name, i.Type, i.BaseCurrency, i.Status, i.CurrentBusinessDate);
    }

    public Task<DateOnly?> GetBusinessDateAsync(Guid institutionId, CancellationToken cancellationToken) =>
        queries.GetBusinessDateAsync(institutionId, cancellationToken);

    public async Task<BranchSummaryDto?> GetBranchAsync(Guid institutionId, Guid branchId, CancellationToken cancellationToken)
    {
        var b = await queries.GetBranchAsync(institutionId, branchId, cancellationToken).ConfigureAwait(false);
        return b is null ? null : new BranchSummaryDto(b.Id, b.InstitutionId, b.Code, b.Name, b.Status);
    }

    async ValueTask<DateOnly?> IBusinessDateProvider.GetBusinessDateAsync(Guid institutionId, CancellationToken cancellationToken) =>
        await queries.GetBusinessDateAsync(institutionId, cancellationToken).ConfigureAwait(false);

    public async Task<bool> IsWorkingDayAsync(Guid institutionId, DateOnly date, CancellationToken cancellationToken) =>
        (await CalendarAsync(institutionId, cancellationToken).ConfigureAwait(false)).IsWorkingDay(date);

    public async Task<DateOnly> NextWorkingDayAsync(Guid institutionId, DateOnly date, CancellationToken cancellationToken) =>
        (await CalendarAsync(institutionId, cancellationToken).ConfigureAwait(false)).NextWorkingDay(date);

    // Institutions without a saved calendar use the platform default (Saturday + Sunday, no holidays).
    private async Task<HolidayCalendar> CalendarAsync(Guid institutionId, CancellationToken cancellationToken)
    {
        var id = new InstitutionId(institutionId);
        return await calendars.GetAsync(id, cancellationToken).ConfigureAwait(false) ?? HolidayCalendar.CreateDefault(id);
    }
}
