using FinCore.BuildingBlocks.Application.Audit;
using FinCore.BuildingBlocks.Application.Messaging;
using FinCore.Modules.Organization.Application.Abstractions;
using FinCore.Modules.Organization.Domain.Errors;
using FinCore.Modules.Organization.Domain.Institutions;
using FinCore.SharedKernel.Results;

namespace FinCore.Modules.Organization.Application.Institutions.ChangeInstitutionStatus;

public enum InstitutionStatusAction
{
    Suspend,
    Reactivate,
    Close,
}

/// <summary>Suspend, reactivate or close an institution (PlatformAdmin only, spec 002).</summary>
public sealed record ChangeInstitutionStatusCommand(Guid InstitutionId, InstitutionStatusAction Action)
    : ICommand<InstitutionResponse>, IAuditableCommand
{
    public string AuditEntityId => InstitutionId.ToString();
}

internal sealed class ChangeInstitutionStatusCommandHandler(
    IInstitutionRepository institutions,
    IBranchRepository branches,
    IOrganizationUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<ChangeInstitutionStatusCommand, InstitutionResponse>
{
    public async Task<Result<InstitutionResponse>> Handle(ChangeInstitutionStatusCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var id = new InstitutionId(command.InstitutionId);
        var institution = await institutions.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (institution is null)
        {
            return OrganizationErrors.InstitutionNotFound(command.InstitutionId);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var result = command.Action switch
        {
            InstitutionStatusAction.Suspend => institution.Suspend(now),
            InstitutionStatusAction.Reactivate => institution.Reactivate(now),
            _ => institution.Close(await branches.HasActiveBranchesAsync(id, cancellationToken).ConfigureAwait(false), now),
        };
        if (result.IsFailure)
        {
            return result.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return InstitutionMapping.ToResponse(institution);
    }
}
