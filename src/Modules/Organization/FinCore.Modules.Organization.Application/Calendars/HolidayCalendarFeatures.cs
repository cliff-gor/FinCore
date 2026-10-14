using FinCore.BuildingBlocks.Application.Audit;
using FinCore.BuildingBlocks.Application.Messaging;
using FinCore.BuildingBlocks.Application.Security;
using FinCore.Modules.Organization.Application.Abstractions;
using FinCore.Modules.Organization.Domain.Calendars;
using FinCore.Modules.Organization.Domain.Errors;
using FinCore.Modules.Organization.Domain.Institutions;
using FinCore.SharedKernel.Results;

namespace FinCore.Modules.Organization.Application.Calendars;

public sealed record HolidayDto(DateOnly Date, string Name);

public sealed record HolidayCalendarResponse(Guid InstitutionId, IReadOnlyList<DayOfWeek> WeekendDays, IReadOnlyList<HolidayDto> Holidays);

/// <param name="Year">Only holidays in this year; null returns all.</param>
public sealed record GetHolidayCalendarQuery(Guid InstitutionId, int? Year) : IQuery<HolidayCalendarResponse>;

/// <summary>Replaces weekend days and future holidays in one call (spec 002); past holidays stay as they were.</summary>
public sealed record ReplaceHolidayCalendarCommand(Guid InstitutionId, IReadOnlyList<DayOfWeek> WeekendDays, IReadOnlyList<HolidayDto> Holidays)
    : ICommand<HolidayCalendarResponse>, IAuditableCommand
{
    public string AuditEntityId => InstitutionId.ToString();
}

internal sealed class GetHolidayCalendarQueryHandler(
    IInstitutionRepository institutions,
    IHolidayCalendarRepository calendars,
    ICurrentUser currentUser) : IQueryHandler<GetHolidayCalendarQuery, HolidayCalendarResponse>
{
    public async Task<Result<HolidayCalendarResponse>> Handle(GetHolidayCalendarQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!currentUser.CanAccessInstitution(query.InstitutionId))
        {
            return OrganizationErrors.InstitutionAccessDenied;
        }

        var id = new InstitutionId(query.InstitutionId);
        if (await institutions.GetAsync(id, cancellationToken).ConfigureAwait(false) is null)
        {
            return OrganizationErrors.InstitutionNotFound(query.InstitutionId);
        }

        // Institutions that never saved a calendar use the platform default (Saturday + Sunday, no holidays).
        var calendar = await calendars.GetAsync(id, cancellationToken).ConfigureAwait(false) ?? HolidayCalendar.CreateDefault(id);
        return HolidayCalendarMapping.ToResponse(calendar, query.Year);
    }
}

internal sealed class ReplaceHolidayCalendarCommandHandler(
    IInstitutionRepository institutions,
    IHolidayCalendarRepository calendars,
    IOrganizationUnitOfWork unitOfWork,
    ICurrentUser currentUser) : ICommandHandler<ReplaceHolidayCalendarCommand, HolidayCalendarResponse>
{
    public async Task<Result<HolidayCalendarResponse>> Handle(ReplaceHolidayCalendarCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!currentUser.CanAccessInstitution(command.InstitutionId))
        {
            return OrganizationErrors.InstitutionAccessDenied;
        }

        var id = new InstitutionId(command.InstitutionId);
        var institution = await institutions.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (institution is null)
        {
            return OrganizationErrors.InstitutionNotFound(command.InstitutionId);
        }

        var calendar = await calendars.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (calendar is null)
        {
            calendar = HolidayCalendar.CreateDefault(id);
            calendars.Add(calendar);
        }

        // "Past" is measured against the institution's business date, not the server clock (spec §9).
        var result = calendar.Replace(
            command.WeekendDays,
            command.Holidays.Select(h => new Holiday(h.Date, h.Name)).ToList(),
            institution.CurrentBusinessDate);
        if (result.IsFailure)
        {
            return result.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return HolidayCalendarMapping.ToResponse(calendar, year: null);
    }
}

internal static class HolidayCalendarMapping
{
    public static HolidayCalendarResponse ToResponse(HolidayCalendar calendar, int? year) => new(
        calendar.InstitutionId.Value,
        calendar.WeekendDays.ToList(),
        calendar.Holidays
            .Where(h => year is null || h.Date.Year == year)
            .OrderBy(h => h.Date)
            .Select(h => new HolidayDto(h.Date, h.Name))
            .ToList());
}
