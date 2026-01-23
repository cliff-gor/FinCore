using FinCore.BuildingBlocks.Infrastructure.Persistence;
using FinCore.Modules.Identity.Domain.Staff;
using FinCore.Modules.Identity.Infrastructure.Persistence;
using FinCore.Modules.Organization.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinCore.Modules.Identity.Infrastructure.DevSeeding;

/// <summary>
/// Development staff accounts (docs/dev-accounts.md, spec 002 AC-22). Roles are granted directly here instead of
/// through maker-checker: this is fixture data for a local machine, and the approval flow itself is what testers
/// exercise afterwards. Never registered outside Development.
/// </summary>
internal sealed class IdentityDevSeeder(
    IdentityDbContext dbContext,
    IOrganizationModuleApi organization,
    TimeProvider timeProvider,
    ILogger<IdentityDevSeeder> logger) : IDevSeeder
{
    private static readonly (string Prefix, string Name, string Branch, Role Role)[] InstitutionStaff =
    [
        ("admin1", "Admin One", "001", Role.SystemAdmin),
        ("admin2", "Admin Two", "001", Role.SystemAdmin),
        ("branch.officer", "Branch Officer", "002", Role.BranchOfficer),
        ("loan.officer", "Loan Officer", "002", Role.LoanOfficer),
        ("field.officer", "Field Officer", "002", Role.FieldOfficer),
        ("credit.approver", "Credit Approver", "001", Role.CreditApprover),
        ("compliance", "Compliance Officer", "001", Role.ComplianceOfficer),
        ("accountant", "Accountant", "001", Role.Accountant),
        ("auditor", "Auditor", "001", Role.Auditor),
    ];

    public int Order => 20;

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        await AddIfMissingAsync("platform.admin@fincore.local", () => StaffUser.RegisterPlatformAdmin("platform.admin@fincore.local", "Platform Admin", now).Value, cancellationToken)
            .ConfigureAwait(false);

        foreach (var code in new[] { "FCB", "FCMFI" })
        {
            var institution = await organization.FindInstitutionByCodeAsync(code, cancellationToken).ConfigureAwait(false);
            if (institution is null)
            {
                logger.LogWarning("Institution {Code} not found; skipping its staff", code);
                continue;
            }

            foreach (var (prefix, name, branchCode, role) in InstitutionStaff)
            {
                var branch = await organization.FindBranchByCodeAsync(institution.Id, branchCode, cancellationToken).ConfigureAwait(false);
                if (branch is null)
                {
                    continue;
                }

                var email = $"{prefix}.{code.ToLowerInvariant()}@fincore.local";
                await AddIfMissingAsync(email, () =>
                {
                    // Same first/last names as the Keycloak realm users (deploy/keycloak), e.g. "Admin One FCB".
                    var user = StaffUser.Register(email, $"{name} {code}", institution.Id, branch.Id).Value;
                    user.GrantRole(role, now);
                    return user;
                }, cancellationToken).ConfigureAwait(false);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task AddIfMissingAsync(string email, Func<StaffUser> create, CancellationToken cancellationToken)
    {
        if (await dbContext.StaffUsers.AnyAsync(s => s.Email == email, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        dbContext.StaffUsers.Add(create());
        logger.LogInformation("Seeded staff {Email}", email);
    }
}
