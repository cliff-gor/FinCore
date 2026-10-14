using FinCore.SharedKernel.Domain;

namespace FinCore.Modules.Organization.Domain.Branches;

public readonly record struct BranchId(Guid Value) : IStronglyTypedId
{
    public static BranchId New() => new(StronglyTypedId.NewValue());
}

public enum BranchStatus
{
    Active = 1,
    Closed = 2,
}
