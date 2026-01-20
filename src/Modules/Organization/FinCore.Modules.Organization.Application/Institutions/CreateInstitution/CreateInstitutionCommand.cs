using FinCore.BuildingBlocks.Application.Messaging;
using FinCore.Modules.Organization.Domain.Institutions;

namespace FinCore.Modules.Organization.Application.Institutions.CreateInstitution;

public sealed record CreateInstitutionCommand(
    string Code,
    string Name,
    InstitutionType Type,
    string BaseCurrency,
    string? LicenseNumber,
    DateOnly InitialBusinessDate) : ICommand<InstitutionResponse>;
