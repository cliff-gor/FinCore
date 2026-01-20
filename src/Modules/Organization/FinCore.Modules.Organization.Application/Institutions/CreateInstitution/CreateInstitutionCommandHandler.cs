using FinCore.BuildingBlocks.Application.Messaging;
using FinCore.BuildingBlocks.Application.Persistence;
using FinCore.Modules.Organization.Application.Abstractions;
using FinCore.Modules.Organization.Domain.Errors;
using FinCore.Modules.Organization.Domain.Institutions;
using FinCore.SharedKernel.Results;

namespace FinCore.Modules.Organization.Application.Institutions.CreateInstitution;

internal sealed class CreateInstitutionCommandHandler(
    IInstitutionRepository institutions,
    IOrganizationUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<CreateInstitutionCommand, InstitutionResponse>
{
    public async Task<Result<InstitutionResponse>> Handle(CreateInstitutionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var normalizedCode = InstitutionCode.Normalize(command.Code);
        if (await institutions.CodeExistsAsync(normalizedCode, cancellationToken).ConfigureAwait(false))
        {
            return OrganizationErrors.InstitutionCodeTaken;
        }

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var created = Institution.Create(
            command.Code,
            command.Name,
            command.Type,
            command.BaseCurrency.Trim().ToUpperInvariant(),
            command.LicenseNumber,
            command.InitialBusinessDate,
            utcNow);
        if (created.IsFailure)
        {
            return created.Error;
        }

        var institution = created.Value;
        institutions.Add(institution);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (UniqueConstraintViolationException)
        {
            // Lost a race with a concurrent create of the same code (spec 001 edge cases).
            return OrganizationErrors.InstitutionCodeTaken;
        }

        return InstitutionMapping.ToResponse(institution);
    }
}
