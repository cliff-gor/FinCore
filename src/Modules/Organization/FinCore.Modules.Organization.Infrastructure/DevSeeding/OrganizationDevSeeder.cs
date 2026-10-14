using FinCore.BuildingBlocks.Application.Messaging;
using FinCore.BuildingBlocks.Infrastructure.Persistence;
using FinCore.Modules.Organization.Application.Branches;
using FinCore.Modules.Organization.Application.Calendars;
using FinCore.Modules.Organization.Application.Institutions.CreateInstitution;
using FinCore.Modules.Organization.Contracts;
using FinCore.Modules.Organization.Domain.Institutions;
using FinCore.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace FinCore.Modules.Organization.Infrastructure.DevSeeding;

/// <summary>
/// Development seed (spec 001, spec 002, docs/dev-accounts.md): FinCore Bank (FCB) and FinCore Microfinance (FCMFI) in
/// KES, branches 001 Head Office and 002 City Branch, and Kenya's remaining 2026 public holidays. Everything goes
/// through the normal command pipeline, so dev data obeys the same rules as data entered through the API.
/// </summary>
internal sealed class OrganizationDevSeeder(IDispatcher dispatcher, IOrganizationModuleApi organization, ILogger<OrganizationDevSeeder> logger)
    : IDevSeeder
{
    // A fixed Tuesday keeps dev data reproducible; it never follows the wall clock.
    private static readonly DateOnly SeedBusinessDate = new(2026, 10, 13);

    // Only holidays on or after the seed business date: earlier ones are immutable history by design (spec 002).
    // Variable religious holidays (Eid) are left out because their dates are announced each year.
    private static readonly HolidayDto[] KenyaHolidays2026 =
    [
        new(new DateOnly(2026, 10, 20), "Mashujaa Day"),
        new(new DateOnly(2026, 12, 12), "Jamhuri Day"),
        new(new DateOnly(2026, 12, 25), "Christmas Day"),
        new(new DateOnly(2026, 12, 26), "Boxing Day"),
    ];

    public int Order => 10;

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        foreach (var (code, name, type) in new[] { ("FCB", "FinCore Bank", InstitutionType.Bank), ("FCMFI", "FinCore Microfinance", InstitutionType.Mfi) })
        {
            var created = await dispatcher.Send(new CreateInstitutionCommand(code, name, type, "KES", null, SeedBusinessDate), cancellationToken)
                .ConfigureAwait(false);
            Report($"institution {code}", created);

            var institution = await organization.FindInstitutionByCodeAsync(code, cancellationToken).ConfigureAwait(false);
            if (institution is null)
            {
                continue;
            }

            Report($"{code} branch 001", await dispatcher.Send(
                new CreateBranchCommand(institution.Id, "001", "Head Office", "Nairobi CBD", null), cancellationToken).ConfigureAwait(false));
            Report($"{code} branch 002", await dispatcher.Send(
                new CreateBranchCommand(institution.Id, "002", "City Branch", "Westlands, Nairobi", null), cancellationToken).ConfigureAwait(false));
            Report($"{code} holidays", await dispatcher.Send(
                new ReplaceHolidayCalendarCommand(institution.Id, [DayOfWeek.Saturday, DayOfWeek.Sunday], KenyaHolidays2026), cancellationToken)
                .ConfigureAwait(false));
        }
    }

    // "Already exists" conflicts are the normal outcome of re-running the seeder; anything else is worth a warning.
    private void Report(string what, Result result)
    {
        if (result.IsSuccess)
        {
            logger.LogInformation("Seeded {What}", what);
        }
        else if (result.Error.Type != ErrorType.Conflict)
        {
            logger.LogWarning("Could not seed {What}: {ErrorCode}", what, result.Error.Code);
        }
    }
}
