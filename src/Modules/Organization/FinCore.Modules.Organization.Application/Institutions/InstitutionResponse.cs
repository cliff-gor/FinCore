namespace FinCore.Modules.Organization.Application.Institutions;

public sealed record InstitutionResponse(
    Guid Id,
    string Code,
    string Name,
    string Type,
    string BaseCurrency,
    string? LicenseNumber,
    string Status,
    DateOnly CurrentBusinessDate,
    DateTime CreatedAtUtc);
