using FinCore.Modules.Organization.Domain.Errors;
using FinCore.Modules.Organization.Domain.Institutions;
using FinCore.SharedKernel.Domain;
using FinCore.SharedKernel.Results;

namespace FinCore.Modules.Organization.Domain.Calendars;

/// <summary>
/// An institution's working-day calendar (spec §12, spec 002): which weekdays are weekends and which dates are
/// public holidays. Loans (due-date shifting) and EOD (business-date advance) depend on it, so past entries are
/// immutable — rewriting history would change already-computed schedules.
/// </summary>
public sealed class HolidayCalendar : AggregateRoot<InstitutionId>
{
    public const int HolidayNameMaxLength = 100;

    private readonly List<Holiday> _holidays = [];

    private HolidayCalendar(InstitutionId institutionId)
        : base(institutionId) =>
        WeekendDays = [DayOfWeek.Saturday, DayOfWeek.Sunday];

    /// <summary>EF Core materialization only.</summary>
    private HolidayCalendar() => WeekendDays = [];

    public InstitutionId InstitutionId => Id;

    public IReadOnlyList<DayOfWeek> WeekendDays { get; private set; }

    public IReadOnlyCollection<Holiday> Holidays => _holidays.AsReadOnly();

    /// <summary>New calendar with the platform default: Saturday and Sunday off, no holidays.</summary>
    public static HolidayCalendar CreateDefault(InstitutionId institutionId) => new(institutionId);

    /// <summary>
    /// Replaces the weekend days and every holiday on or after <paramref name="currentBusinessDate"/>. Holidays before
    /// the business date are kept untouched and may not appear in the new list.
    /// </summary>
    public Result Replace(IReadOnlyCollection<DayOfWeek> weekendDays, IReadOnlyCollection<Holiday> futureHolidays, DateOnly currentBusinessDate)
    {
        ArgumentNullException.ThrowIfNull(weekendDays);
        ArgumentNullException.ThrowIfNull(futureHolidays);

        var weekends = weekendDays.Distinct().Order().ToList();
        // At least one working day per week, or no date could ever be a business date.
        if (weekends.Count >= 7 || weekends.Any(d => !Enum.IsDefined(d)))
        {
            return OrganizationErrors.InvalidWeekendDays;
        }

        if (futureHolidays.Any(h => h.Date < currentBusinessDate))
        {
            return OrganizationErrors.PastHolidaysAreImmutable;
        }

        if (futureHolidays.Any(h => string.IsNullOrWhiteSpace(h.Name) || h.Name.Trim().Length > HolidayNameMaxLength)
            || futureHolidays.GroupBy(h => h.Date).Any(g => g.Count() > 1))
        {
            return OrganizationErrors.InvalidHolidays;
        }

        WeekendDays = weekends;
        _holidays.RemoveAll(h => h.Date >= currentBusinessDate);
        _holidays.AddRange(futureHolidays.Select(h => h with { Name = h.Name.Trim() }));
        return Result.Success();
    }

    public bool IsWorkingDay(DateOnly date) =>
        !WeekendDays.Contains(date.DayOfWeek) && _holidays.All(h => h.Date != date);

    /// <summary>The first working day strictly after <paramref name="date"/> (used for due-date shifting and EOD).</summary>
    public DateOnly NextWorkingDay(DateOnly date)
    {
        // Bounded: a week always has at least one working day and holidays are finite, so this terminates quickly.
        var candidate = date.AddDays(1);
        while (!IsWorkingDay(candidate))
        {
            candidate = candidate.AddDays(1);
        }

        return candidate;
    }
}

/// <summary>A public holiday on a specific date.</summary>
public sealed record Holiday(DateOnly Date, string Name) : IValueObject;
