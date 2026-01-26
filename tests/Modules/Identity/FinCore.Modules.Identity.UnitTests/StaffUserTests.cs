using FinCore.Modules.Identity.Domain.Errors;
using FinCore.Modules.Identity.Domain.Staff;

namespace FinCore.Modules.Identity.UnitTests;

/// <summary>Staff access rules from spec 002: registration, linking, roles, limits and (re)activation.</summary>
public sealed class StaffUserTests
{
    private static readonly DateTime Now = new(2026, 10, 14, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Institution = Guid.CreateVersion7();
    private static readonly Guid Branch = Guid.CreateVersion7();

    private static StaffUser NewUser(string email = " Jane.Doe@Bank.Test ") =>
        StaffUser.Register(email, "Jane Doe", Institution, Branch).Value;

    [Fact]
    public void Register_normalizes_email_and_starts_pending_with_no_roles()
    {
        var user = NewUser();

        user.Email.ShouldBe("jane.doe@bank.test");
        user.Status.ShouldBe(StaffStatus.PendingFirstLogin);
        user.Roles.ShouldBeEmpty();
        user.Permissions.ShouldBeEmpty();
        user.IsActive.ShouldBeFalse();
    }

    [Theory]
    [InlineData("not-an-email", "Jane Doe")]
    [InlineData("", "Jane Doe")]
    [InlineData("jane@bank.test", "J")]
    public void Register_rejects_invalid_input(string email, string name)
    {
        StaffUser.Register(email, name, Institution, Branch).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Linking_activates_once_and_never_relinks_to_another_subject()
    {
        var user = NewUser();

        user.LinkIdentity("sub-1", Now).IsSuccess.ShouldBeTrue();
        user.Status.ShouldBe(StaffStatus.Active);
        user.LinkIdentity("sub-1", Now).IsSuccess.ShouldBeTrue();
        user.LinkIdentity("sub-2", Now).Error.ShouldBe(IdentityErrors.StaffAlreadyLinked);
    }

    [Fact]
    public void Deactivated_user_cannot_link()
    {
        var user = NewUser();
        user.Deactivate(Now);
        user.LinkIdentity("sub-1", Now).Error.ShouldBe(IdentityErrors.StaffInactive);
    }

    [Fact]
    public void Granting_and_removing_roles_changes_permissions_and_raises_events()
    {
        var user = NewUser();

        user.GrantRole(Role.LoanOfficer, Now).IsSuccess.ShouldBeTrue();
        user.Permissions.ShouldContain("loans.apply");
        user.GrantRole(Role.LoanOfficer, Now).Error.ShouldBe(IdentityErrors.RoleAlreadyGranted);

        user.RemoveRole(Role.LoanOfficer, Now).IsSuccess.ShouldBeTrue();
        user.Permissions.ShouldNotContain("loans.apply");
        user.RemoveRole(Role.LoanOfficer, Now).Error.ShouldBe(IdentityErrors.RoleNotGranted);
        user.DomainEvents.Count.ShouldBe(2);
    }

    [Fact]
    public void PlatformAdmin_cannot_be_granted_at_runtime()
    {
        NewUser().GrantRole(Role.PlatformAdmin, Now).Error.ShouldBe(IdentityErrors.RoleNotAssignable);
    }

    [Fact]
    public void Platform_admin_has_platform_permissions_and_no_institution()
    {
        var admin = StaffUser.RegisterPlatformAdmin("ops@fincore.test", "Ops", Now).Value;

        admin.IsPlatformAdmin.ShouldBeTrue();
        admin.InstitutionId.ShouldBeNull();
        admin.Permissions.ShouldContain("platform.institutions.manage");
        admin.GrantRole(Role.Auditor, Now).Error.ShouldBe(IdentityErrors.RoleNotAssignable);
    }

    [Fact]
    public void Approval_limits_can_be_set_lowered_and_cleared()
    {
        var user = NewUser();

        user.SetApprovalLimit(ApprovalLimitType.LoanApproval, 500_000m, Now).IsSuccess.ShouldBeTrue();
        user.ApprovalLimitFor(ApprovalLimitType.LoanApproval).ShouldBe(500_000m);
        user.SetApprovalLimit(ApprovalLimitType.LoanApproval, 0m, Now).IsSuccess.ShouldBeTrue();
        user.ApprovalLimits.ShouldBeEmpty();
        user.SetApprovalLimit(ApprovalLimitType.LoanApproval, -1m, Now).Error.ShouldBe(IdentityErrors.InvalidApprovalLimit);
    }

    [Fact]
    public void Deactivation_removes_all_permissions_and_reactivation_restores_no_roles()
    {
        var user = NewUser();
        user.LinkIdentity("sub-1", Now);
        user.GrantRole(Role.Accountant, Now);

        user.Deactivate(Now).IsSuccess.ShouldBeTrue();
        user.Permissions.ShouldBeEmpty();
        user.Deactivate(Now).Error.ShouldBe(IdentityErrors.StaffInactive);
        user.GrantRole(Role.Auditor, Now).Error.ShouldBe(IdentityErrors.StaffInactive);
        user.SetApprovalLimit(ApprovalLimitType.Withdrawal, 1m, Now).Error.ShouldBe(IdentityErrors.StaffInactive);

        user.Reactivate(Now).IsSuccess.ShouldBeTrue();
        user.Status.ShouldBe(StaffStatus.Active);
        user.Roles.ShouldBeEmpty(); // spec 002: old privileges are never restored silently
        user.Reactivate(Now).Error.ShouldBe(IdentityErrors.StaffNotDeactivated);
    }

    [Fact]
    public void Reactivating_an_unlinked_user_returns_to_pending()
    {
        var user = NewUser();
        user.Deactivate(Now);
        user.Reactivate(Now);
        user.Status.ShouldBe(StaffStatus.PendingFirstLogin);
    }

    [Fact]
    public void Role_permissions_match_spec_13()
    {
        RolePermissions.For(Role.SystemAdmin).ShouldContain("identity.approve");
        RolePermissions.For(Role.Auditor).ShouldContain("ledger.read");
        RolePermissions.For(Role.Auditor).ShouldContain("scope.all-branches");
        RolePermissions.For(Role.BranchOfficer).ShouldNotContain("scope.all-branches");
        RolePermissions.For([Role.LoanOfficer, Role.CreditApprover]).ShouldBe(
            ["loans.apply", "loans.approve", "loans.assess", "loans.repay", "cards.limit.approve"], ignoreOrder: true);
    }
}
