using FinCore.SharedKernel.Domain;

namespace FinCore.Modules.Organization.Domain.Institutions;

public readonly record struct InstitutionId(Guid Value) : IStronglyTypedId
{
    public static InstitutionId New() => new(StronglyTypedId.NewValue());
}
