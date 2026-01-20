namespace FinCore.Modules.Organization.Contracts.Dtos;

public sealed record InstitutionSummaryDto(
    Guid Id,
    string Code,
    string Name,
    string Type,
    string BaseCurrency,
    string Status,
    DateOnly CurrentBusinessDate);
