namespace FinCore.Modules.Organization.Contracts.Dtos;

public sealed record BranchSummaryDto(Guid Id, Guid InstitutionId, string Code, string Name, string Status);
