using FinCore.Modules.Identity.Domain.Approvals;
using FinCore.Modules.Identity.Domain.Errors;
using FinCore.Modules.Identity.Domain.Institutions;

namespace FinCore.Modules.Identity.UnitTests;

/// <summary>The maker-checker state machine (spec 002): four-eyes, checker count, expiry and outcomes.</summary>
public sealed class ApprovalRequestTests
{
    private static readonly DateTime Now = new(2026, 10, 14, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Maker = Guid.CreateVersion7();
    private static readonly Guid Subject = Guid.CreateVersion7();
    private static readonly Guid CheckerA = Guid.CreateVersion7();
    private static readonly Guid CheckerB = Guid.CreateVersion7();

    private static ApprovalRequest NewRequest(int requiredCheckers = 1) =>
        ApprovalRequest.Create(Guid.CreateVersion7(), "Identity", IdentityOperations.GrantRole, Subject.ToString(), Subject,
            null, null, "{}", Maker, requiredCheckers, Now.AddDays(7));

    [Fact]
    public void Single_checker_approval_then_complete_publishes_approved()
    {
        var request = NewRequest();

        request.Approve(CheckerA, "ok", Now).IsSuccess.ShouldBeTrue();
        request.Status.ShouldBe(ApprovalStatus.Approved);
        request.DomainEvents.ShouldBeEmpty(); // the outcome is published only after execution

        request.Complete(Now);
        var decided = request.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<ApprovalRequestDecidedDomainEvent>();
        decided.Outcome.ShouldBe(ApprovalStatus.Approved);
        decided.DecidedBy.ShouldBe([CheckerA]);
        request.CompletedAtUtc.ShouldBe(Now);
    }

    [Fact]
    public void Two_checker_rule_needs_two_distinct_people()
    {
        var request = NewRequest(requiredCheckers: 2);

        request.Approve(CheckerA, null, Now).IsSuccess.ShouldBeTrue();
        request.Status.ShouldBe(ApprovalStatus.Pending);
        request.Approve(CheckerA, null, Now).Error.ShouldBe(IdentityErrors.AlreadyDecidedByChecker);
        request.Approve(CheckerB, null, Now).IsSuccess.ShouldBeTrue();
        request.Status.ShouldBe(ApprovalStatus.Approved);
        request.ApprovalCount.ShouldBe(2);
    }

    [Fact]
    public void Platform_admin_approval_completes_alone()
    {
        var request = NewRequest(requiredCheckers: 2);
        request.Approve(CheckerA, null, Now, completesAlone: true).IsSuccess.ShouldBeTrue();
        request.Status.ShouldBe(ApprovalStatus.Approved);
    }

    [Fact]
    public void Maker_and_subject_can_never_decide()
    {
        var request = NewRequest();
        request.Approve(Maker, null, Now).Error.ShouldBe(IdentityErrors.FourEyesViolation);
        request.Approve(Subject, null, Now).Error.ShouldBe(IdentityErrors.FourEyesViolation);
    }

    [Fact]
    public void One_rejection_rejects_and_needs_a_reason()
    {
        var request = NewRequest(requiredCheckers: 2);

        request.Reject(CheckerA, "  ", Now).Error.ShouldBe(IdentityErrors.RejectionReasonRequired);
        request.Reject(CheckerA, "Not justified", Now).IsSuccess.ShouldBeTrue();
        request.Status.ShouldBe(ApprovalStatus.Rejected);
        request.OutcomeReason.ShouldBe("Not justified");
        request.Approve(CheckerB, null, Now).Error.ShouldBe(IdentityErrors.ApprovalAlreadyDecided);
    }

    [Fact]
    public void Only_the_maker_can_withdraw_a_pending_request()
    {
        var request = NewRequest();
        request.Withdraw(CheckerA, Now).Error.ShouldBe(IdentityErrors.OnlyMakerCanWithdraw);
        request.Withdraw(Maker, Now).IsSuccess.ShouldBeTrue();
        request.Status.ShouldBe(ApprovalStatus.Withdrawn);
        request.Withdraw(Maker, Now).Error.ShouldBe(IdentityErrors.ApprovalAlreadyDecided);
    }

    [Fact]
    public void Expiry_happens_only_when_due_and_blocks_late_decisions()
    {
        var request = NewRequest();

        request.ExpireIfDue(Now).ShouldBeFalse();
        request.Approve(CheckerA, null, Now.AddDays(7)).Error.ShouldBe(IdentityErrors.ApprovalExpired);
        request.ExpireIfDue(Now.AddDays(7)).ShouldBeTrue();
        request.Status.ShouldBe(ApprovalStatus.Expired);
        request.ExpireIfDue(Now.AddDays(8)).ShouldBeFalse();
    }

    [Fact]
    public void Failed_execution_records_the_reason()
    {
        var request = NewRequest();
        request.Approve(CheckerA, null, Now);

        request.Fail("Identity.StaffInactive", Now);

        request.Status.ShouldBe(ApprovalStatus.Failed);
        request.OutcomeReason.ShouldBe("Identity.StaffInactive");
    }

    [Fact]
    public void Complete_and_fail_require_an_approved_request()
    {
        Should.Throw<InvalidOperationException>(() => NewRequest().Complete(Now));
        Should.Throw<InvalidOperationException>(() => NewRequest().Fail("x", Now));
        Should.Throw<ArgumentOutOfRangeException>(() => NewRequest(requiredCheckers: 0));
    }

    [Fact]
    public void Long_comments_are_truncated_to_the_column_size()
    {
        var request = NewRequest();
        request.Approve(CheckerA, new string('x', 600), Now);
        request.Decisions.Single().Comment!.Length.ShouldBe(ApprovalRequest.ReasonMaxLength);
    }

    [Fact]
    public void Institution_snapshot_tracks_status()
    {
        var snapshot = new InstitutionSnapshot(Guid.CreateVersion7(), "FCB", "Active", "KES");
        snapshot.IsClosed.ShouldBeFalse();
        snapshot.ChangeStatus("Closed", Now);
        snapshot.IsClosed.ShouldBeTrue();
        snapshot.UpdatedAtUtc.ShouldBe(Now);
    }
}
