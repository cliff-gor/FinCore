using FinCore.SharedKernel.Domain;

namespace FinCore.Modules.Identity.Domain.Staff;

public readonly record struct StaffUserId(Guid Value) : IStronglyTypedId
{
    public static StaffUserId New() => new(StronglyTypedId.NewValue());
}

/// <summary>
/// <c>PendingFirstLogin</c> until the identity provider account is linked on first sign-in (spec 002); then
/// <c>Active</c> ⇄ <c>Deactivated</c>.
/// </summary>
public enum StaffStatus
{
    PendingFirstLogin = 1,
    Active = 2,
    Deactivated = 3,
}

/// <summary>Kinds of monetary authority a checker can hold (master §13).</summary>
public enum ApprovalLimitType
{
    LoanApproval = 1,
    CardLimit = 2,
    ManualJournal = 3,
    Withdrawal = 4,
}
