using FinCore.Modules.Organization.Domain.Branches;
using FinCore.Modules.Organization.Domain.Calendars;
using FinCore.Modules.Organization.Domain.Errors;
using FinCore.Modules.Organization.Domain.Institutions;

namespace FinCore.Modules.Organization.UnitTests;

/// <summary>Branches, holiday calendars and institution lifecycle (spec 002 AC-7 to AC-9, domain part).</summary>
public sealed class BranchAndCalendarTests
{
    private static readonly DateTime Now = new(2026, 10, 14, 9, 0, 0, DateTimeKind.Utc);
    private static readonly InstitutionId Bank = InstitutionId.New();

    private static Branch NewBranch(string code = "001", Branch? parent = null, InstitutionId? institution = null) =>
        Branch.Create(institution ?? Bank, code, "Head Office", " Nairobi ", parent, Now).Value;

    [Theory]
    [InlineData("01")]
    [InlineData("0001")]
    [InlineData("A01")]
    [InlineData(null)]
    public void Branch_codes_are_exactly_three_digits(string? code)
    {
        Branch.Create(Bank, code, "Head Office", null, null, Now).Error.ShouldBe(OrganizationErrors.InvalidBranchCode);
    }

    [Fact]
    public void Create_trims_and_raises_event()
    {
        var branch = NewBranch();
        branch.Code.Value.ShouldBe("001");
        branch.Address.ShouldBe("Nairobi");
        branch.Status.ShouldBe(BranchStatus.Active);
        branch.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<BranchCreatedDomainEvent>();
        BranchCode.FromStorage("002").ToString().ShouldBe("002");
    }

    [Fact]
    public void Hierarchy_is_at_most_two_levels_and_within_the_institution()
    {
        var head = NewBranch("001");
        var child = NewBranch("002", head);
        child.ParentBranchId.ShouldBe(head.Id);

        // A child cannot be a parent, and parents must belong to the same institution.
        Branch.Create(Bank, "003", "Deep", null, child, Now).Error.ShouldBe(OrganizationErrors.InvalidParentBranch);
        Branch.Create(InstitutionId.New(), "003", "Other", null, head, Now).Error.ShouldBe(OrganizationErrors.InvalidParentBranch);

        // A branch with children cannot get a parent, and cannot parent itself.
        head.Update("Head Office", null, NewBranch("004"), hasChildren: true).Error.ShouldBe(OrganizationErrors.InvalidParentBranch);
        head.Update("Head Office", null, head, hasChildren: false).Error.ShouldBe(OrganizationErrors.InvalidParentBranch);
    }

    [Fact]
    public void Name_and_address_are_validated()
    {
        Branch.Create(Bank, "001", "X", null, null, Now).Error.ShouldBe(OrganizationErrors.InvalidBranchName);
        Branch.Create(Bank, "001", "Head", new string('a', 301), null, Now).Error.ShouldBe(OrganizationErrors.InvalidBranchAddress);
        var branch = NewBranch();
        branch.Update("Renamed", "New address", null, hasChildren: false).IsSuccess.ShouldBeTrue();
        branch.Name.ShouldBe("Renamed");
    }

    [Fact]
    public void Closing_requires_no_active_staff_or_children()
    {
        var branch = NewBranch();
        branch.Close(hasActiveStaff: true, hasActiveChildren: false, Now).Error.ShouldBe(OrganizationErrors.BranchHasActiveStaff);
        branch.Close(hasActiveStaff: false, hasActiveChildren: true, Now).Error.ShouldBe(OrganizationErrors.BranchHasActiveChildren);
        branch.Close(hasActiveStaff: false, hasActiveChildren: false, Now).IsSuccess.ShouldBeTrue();
        branch.Close(false, false, Now).Error.ShouldBe(OrganizationErrors.BranchClosed);
        branch.Update("Name", null, null, false).Error.ShouldBe(OrganizationErrors.BranchClosed);
    }

    [Fact]
    public void Calendar_defaults_to_saturday_and_sunday()
    {
        var calendar = HolidayCalendar.CreateDefault(Bank);
        calendar.IsWorkingDay(new DateOnly(2026, 10, 17)).ShouldBeFalse(); // Saturday
        calendar.IsWorkingDay(new DateOnly(2026, 10, 16)).ShouldBeTrue();  // Friday
        calendar.NextWorkingDay(new DateOnly(2026, 10, 16)).ShouldBe(new DateOnly(2026, 10, 19));
        calendar.InstitutionId.ShouldBe(Bank);
    }

    [Fact]
    public void Calendar_replace_honours_custom_weekends_and_holidays()
    {
        var calendar = HolidayCalendar.CreateDefault(Bank);
        var businessDate = new DateOnly(2026, 10, 14);

        calendar.Replace([DayOfWeek.Friday, DayOfWeek.Saturday], [new Holiday(new DateOnly(2026, 10, 20), " Mashujaa Day ")], businessDate)
            .IsSuccess.ShouldBeTrue();

        calendar.IsWorkingDay(new DateOnly(2026, 10, 16)).ShouldBeFalse(); // Friday is now a weekend day
        calendar.IsWorkingDay(new DateOnly(2026, 10, 18)).ShouldBeTrue();  // Sunday is a working day
        calendar.NextWorkingDay(new DateOnly(2026, 10, 19)).ShouldBe(new DateOnly(2026, 10, 21)); // skips the holiday
        calendar.Holidays.Single().Name.ShouldBe("Mashujaa Day");
    }

    [Fact]
    public void Calendar_rejects_past_duplicate_and_impossible_input()
    {
        var calendar = HolidayCalendar.CreateDefault(Bank);
        var businessDate = new DateOnly(2026, 10, 14);
        var everyDay = Enum.GetValues<DayOfWeek>();

        calendar.Replace(everyDay, [], businessDate).Error.ShouldBe(OrganizationErrors.InvalidWeekendDays);
        calendar.Replace([], [new Holiday(new DateOnly(2026, 1, 1), "New Year")], businessDate).Error.ShouldBe(OrganizationErrors.PastHolidaysAreImmutable);
        calendar.Replace([], [new Holiday(new DateOnly(2026, 12, 25), "A"), new Holiday(new DateOnly(2026, 12, 25), "B")], businessDate)
            .Error.ShouldBe(OrganizationErrors.InvalidHolidays);
        calendar.Replace([], [new Holiday(new DateOnly(2026, 12, 25), " ")], businessDate).Error.ShouldBe(OrganizationErrors.InvalidHolidays);
    }

    [Fact]
    public void Calendar_keeps_past_holidays_when_replacing_future_ones()
    {
        var calendar = HolidayCalendar.CreateDefault(Bank);
        calendar.Replace([], [new Holiday(new DateOnly(2026, 10, 20), "Mashujaa Day")], new DateOnly(2026, 10, 14));

        // Later, after the business date moved past the 20th, a new list replaces only the future.
        calendar.Replace([], [new Holiday(new DateOnly(2026, 12, 25), "Christmas")], new DateOnly(2026, 11, 2)).IsSuccess.ShouldBeTrue();

        calendar.Holidays.Select(h => h.Date).ShouldBe([new DateOnly(2026, 10, 20), new DateOnly(2026, 12, 25)], ignoreOrder: true);
    }

    [Fact]
    public void Institution_lifecycle_follows_the_state_machine()
    {
        var institution = Institution.Create("FCB", "FinCore Bank", InstitutionType.Bank, "KES", null, new DateOnly(2026, 10, 14), Now).Value;
        institution.ClearDomainEvents();

        institution.Reactivate(Now).IsFailure.ShouldBeTrue();
        institution.Suspend(Now).IsSuccess.ShouldBeTrue();
        institution.Status.ShouldBe(InstitutionStatus.Suspended);
        institution.Suspend(Now).IsFailure.ShouldBeTrue();
        institution.Reactivate(Now).IsSuccess.ShouldBeTrue();

        institution.Close(hasActiveBranches: true, Now).Error.ShouldBe(OrganizationErrors.InstitutionHasActiveBranches);
        institution.Close(hasActiveBranches: false, Now).IsSuccess.ShouldBeTrue();
        institution.Close(hasActiveBranches: false, Now).IsFailure.ShouldBeTrue();

        institution.DomainEvents.OfType<InstitutionStatusChangedDomainEvent>().Count().ShouldBe(3);
        institution.DomainEvents.OfType<InstitutionStatusChangedDomainEvent>().Last().Status.ShouldBe(InstitutionStatus.Closed);
    }
}
