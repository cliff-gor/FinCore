using FinCore.Modules.Organization.Domain.Institutions;

namespace FinCore.Modules.Organization.Application.Institutions;

internal static class InstitutionMapping
{
    public static InstitutionResponse ToResponse(Institution institution) => new(
        institution.Id.Value,
        institution.Code.Value,
        institution.Name,
        institution.Type.ToString(),
        institution.BaseCurrency,
        institution.LicenseNumber,
        institution.Status.ToString(),
        institution.CurrentBusinessDate,
        institution.CreatedAtUtc);
}
