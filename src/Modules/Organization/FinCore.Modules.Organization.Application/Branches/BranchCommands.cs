using FinCore.BuildingBlocks.Application.Audit;
using FinCore.BuildingBlocks.Application.Messaging;
using FinCore.BuildingBlocks.Application.Persistence;
using FinCore.BuildingBlocks.Application.Security;
using FinCore.Modules.Identity.Contracts;
using FinCore.Modules.Organization.Application.Abstractions;
using FinCore.Modules.Organization.Domain.Branches;
using FinCore.Modules.Organization.Domain.Errors;
using FinCore.Modules.Organization.Domain.Institutions;
using FinCore.SharedKernel.Results;
using FluentValidation;

namespace FinCore.Modules.Organization.Application.Branches;

public sealed record CreateBranchCommand(Guid InstitutionId, string Code, string Name, string? Address, Guid? ParentBranchId)
    : ICommand<BranchResponse>, IAuditableCommand
{
    public string AuditEntityId => $"{InstitutionId}/{Code}";
}

public sealed record UpdateBranchCommand(Guid InstitutionId, Guid BranchId, string Name, string? Address, Guid? ParentBranchId)
    : ICommand<BranchResponse>, IAuditableCommand
{
    public string AuditEntityId => BranchId.ToString();
}

public sealed record CloseBranchCommand(Guid InstitutionId, Guid BranchId) : ICommand<BranchResponse>, IAuditableCommand
{
    public string AuditEntityId => BranchId.ToString();
}

/// <summary>Field-level checks so clients get every error at once; the aggregate re-checks the same invariants.</summary>
internal sealed class CreateBranchCommandValidator : AbstractValidator<CreateBranchCommand>
{
    public CreateBranchCommandValidator()
    {
        RuleFor(c => c.Code).Must(BranchCode.IsValid).WithMessage($"Branch code must be exactly {BranchCode.Length} digits.");
        RuleFor(c => c.Name).Must(n => (n?.Trim().Length ?? 0) is >= Branch.NameMinLength and <= Branch.NameMaxLength)
            .WithMessage($"Name must be {Branch.NameMinLength}–{Branch.NameMaxLength} characters.");
        RuleFor(c => c.Address).MaximumLength(Branch.AddressMaxLength)
            .WithMessage($"Address must be at most {Branch.AddressMaxLength} characters.");
    }
}

internal sealed class CreateBranchCommandHandler(
    IInstitutionRepository institutions,
    IBranchRepository branches,
    IOrganizationUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : ICommandHandler<CreateBranchCommand, BranchResponse>
{
    public async Task<Result<BranchResponse>> Handle(CreateBranchCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!currentUser.CanAccessInstitution(command.InstitutionId))
        {
            return OrganizationErrors.InstitutionAccessDenied;
        }

        var institutionId = new InstitutionId(command.InstitutionId);
        if (await institutions.GetAsync(institutionId, cancellationToken).ConfigureAwait(false) is null)
        {
            return OrganizationErrors.InstitutionNotFound(command.InstitutionId);
        }

        // Fast path only; the unique index (institution_id, code) catches concurrent duplicates below.
        if (await branches.CodeExistsAsync(institutionId, command.Code.Trim(), cancellationToken).ConfigureAwait(false))
        {
            return OrganizationErrors.BranchCodeTaken;
        }

        Branch? parent = null;
        if (command.ParentBranchId is { } parentId)
        {
            parent = await branches.GetAsync(new BranchId(parentId), cancellationToken).ConfigureAwait(false);
            if (parent is null)
            {
                return OrganizationErrors.InvalidParentBranch;
            }
        }

        var created = Branch.Create(institutionId, command.Code, command.Name, command.Address, parent, timeProvider.GetUtcNow().UtcDateTime);
        if (created.IsFailure)
        {
            return created.Error;
        }

        branches.Add(created.Value);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (UniqueConstraintViolationException)
        {
            return OrganizationErrors.BranchCodeTaken;
        }

        return BranchMapping.ToResponse(created.Value);
    }
}

internal sealed class UpdateBranchCommandHandler(
    IBranchRepository branches,
    IOrganizationUnitOfWork unitOfWork,
    ICurrentUser currentUser) : ICommandHandler<UpdateBranchCommand, BranchResponse>
{
    public async Task<Result<BranchResponse>> Handle(UpdateBranchCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!currentUser.CanAccessInstitution(command.InstitutionId))
        {
            return OrganizationErrors.InstitutionAccessDenied;
        }

        var branch = await branches.GetAsync(new BranchId(command.BranchId), cancellationToken).ConfigureAwait(false);
        // A branch of another institution is reported as not found rather than leaking that it exists.
        if (branch is null || branch.InstitutionId.Value != command.InstitutionId)
        {
            return OrganizationErrors.BranchNotFound(command.BranchId);
        }

        Branch? parent = null;
        if (command.ParentBranchId is { } parentId)
        {
            parent = await branches.GetAsync(new BranchId(parentId), cancellationToken).ConfigureAwait(false);
            if (parent is null)
            {
                return OrganizationErrors.InvalidParentBranch;
            }
        }

        var hasChildren = await branches.HasChildrenAsync(branch.Id, cancellationToken).ConfigureAwait(false);
        var result = branch.Update(command.Name, command.Address, parent, hasChildren);
        if (result.IsFailure)
        {
            return result.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return BranchMapping.ToResponse(branch);
    }
}

internal sealed class CloseBranchCommandHandler(
    IBranchRepository branches,
    IIdentityModuleApi identity,
    IOrganizationUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : ICommandHandler<CloseBranchCommand, BranchResponse>
{
    public async Task<Result<BranchResponse>> Handle(CloseBranchCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!currentUser.CanAccessInstitution(command.InstitutionId))
        {
            return OrganizationErrors.InstitutionAccessDenied;
        }

        var branch = await branches.GetAsync(new BranchId(command.BranchId), cancellationToken).ConfigureAwait(false);
        if (branch is null || branch.InstitutionId.Value != command.InstitutionId)
        {
            return OrganizationErrors.BranchNotFound(command.BranchId);
        }

        // Staff are owned by Identity, so ask through its read-only contract (spec §10).
        var hasActiveStaff = await identity.HasActiveStaffAtBranchAsync(command.BranchId, cancellationToken).ConfigureAwait(false);
        var hasActiveChildren = await branches.HasActiveChildrenAsync(branch.Id, cancellationToken).ConfigureAwait(false);
        var result = branch.Close(hasActiveStaff, hasActiveChildren, timeProvider.GetUtcNow().UtcDateTime);
        if (result.IsFailure)
        {
            return result.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return BranchMapping.ToResponse(branch);
    }
}
