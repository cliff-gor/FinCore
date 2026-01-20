using FinCore.BuildingBlocks.Application.Messaging;
using FinCore.Modules.Organization.Application.Institutions.CreateInstitution;
using FinCore.Modules.Organization.Domain.Institutions;
using FinCore.SharedKernel.Results;

namespace FinCore.Api.DevSeeding;

/// <summary>
/// Development seed data (spec 001): FinCore Bank (FCB) and FinCore Microfinance (FCMFI) in KES.
/// Goes through the normal command pipeline; existing codes are skipped, so it is idempotent.
/// </summary>
internal sealed class DevSeeder(IDispatcher dispatcher, ILogger<DevSeeder> logger)
{
    // A fixed date (a Tuesday) keeps dev data reproducible; it never follows the wall clock.
    private static readonly DateOnly SeedBusinessDate = new(2026, 10, 13);

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        CreateInstitutionCommand[] institutions =
        [
            new("FCB", "FinCore Bank", InstitutionType.Bank, "KES", null, SeedBusinessDate),
            new("FCMFI", "FinCore Microfinance", InstitutionType.Mfi, "KES", null, SeedBusinessDate),
        ];

        foreach (var command in institutions)
        {
            // Seeding goes through the normal pipeline (validation, outbox event), so dev data looks exactly like
            // data created through the API.
            var result = await dispatcher.Send(command, cancellationToken).ConfigureAwait(false);
            if (result.IsSuccess)
            {
                logger.LogInformation("Seeded institution {Code}", command.Code);
            }
            else if (result.Error.Type != ErrorType.Conflict)
            {
                logger.LogWarning("Could not seed institution {Code}: {ErrorCode}", command.Code, result.Error.Code);
            }
        }
    }
}
