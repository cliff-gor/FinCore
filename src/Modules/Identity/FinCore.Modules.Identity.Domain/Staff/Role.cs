namespace FinCore.Modules.Identity.Domain.Staff;

/// <summary>The fixed roles of M1 (master spec §13, spec 002). Not editable at runtime; custom roles are deferred.</summary>
public enum Role
{
    PlatformAdmin = 1,
    SystemAdmin = 2,
    BranchOfficer = 3,
    LoanOfficer = 4,
    FieldOfficer = 5,
    CreditApprover = 6,
    ComplianceOfficer = 7,
    Accountant = 8,
    Auditor = 9,
}

/// <summary>
/// Role → permission mapping. This is the single source of truth for what each role may do; the API turns a user's
/// roles into permission claims from here on every request (cached), so a change here applies to everyone at once.
/// Permission strings must match the constants the endpoints use (BuildingBlocks.Web Permissions) and the front ends.
/// </summary>
public static class RolePermissions
{
    // Every read permission the platform will have; Auditors get all of them (master §13: "*.read").
    private static readonly string[] AllReadPermissions =
    [
        "org.read", "customers.read", "accounts.read", "loans.read", "mfi.read", "cards.read",
        "ledger.read", "eod.read", "reports.read",
    ];

    private static readonly Dictionary<Role, string[]> Map = new()
    {
        // Platform-level: not tied to an institution; may act on any institution and approve as fallback checker.
        [Role.PlatformAdmin] =
        [
            "platform.institutions.manage", "org.read", "org.manage", "identity.manage", "identity.approve",
            "scope.all-branches",
        ],
        [Role.SystemAdmin] = ["org.read", "org.manage", "identity.manage", "identity.approve", "products.manage", "scope.all-branches"],
        [Role.BranchOfficer] = ["customers.create", "kyc.capture", "accounts.open", "accounts.transact"],
        [Role.LoanOfficer] = ["loans.apply", "loans.assess", "loans.repay"],
        [Role.FieldOfficer] = ["mfi.groups.manage", "mfi.collections.record"],
        [Role.CreditApprover] = ["loans.approve", "cards.limit.approve"],
        [Role.ComplianceOfficer] = ["kyc.approve", "aml.review"],
        [Role.Accountant] = ["ledger.journal.create", "ledger.read", "eod.run"],
        // Auditors must be able to look everywhere in their institution, so they also get all-branch scope.
        [Role.Auditor] = [.. AllReadPermissions, "scope.all-branches"],
    };

    public static IReadOnlyCollection<string> For(Role role) => Map[role];

    public static IReadOnlyCollection<string> For(IEnumerable<Role> roles) =>
        roles.SelectMany(r => Map[r]).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
}
