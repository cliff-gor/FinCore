using FinCore.BuildingBlocks.Application.Audit;
using FinCore.BuildingBlocks.Application.Messaging;
using FinCore.BuildingBlocks.Application.Persistence;
using FinCore.BuildingBlocks.Application.Security;
using FinCore.Modules.Identity.Application.Abstractions;
using FinCore.Modules.Identity.Application.Approvals;
using FinCore.Modules.Identity.Domain.Approvals;
using FinCore.Modules.Identity.Domain.Errors;
using FinCore.Modules.Identity.Domain.Staff;
using FinCore.Modules.Organization.Contracts;
using FinCore.SharedKernel.Results;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace FinCore.Modules.Identity.Application.Staff;

/// <param name="InstitutionId">PlatformAdmin only: the institution to register into. Staff of an institution always register into their own.</param>
/// <param name="BootstrapSystemAdmin">PlatformAdmin only: register an institution's first SystemAdmin with the role (spec 002 bootstrap exception).</param>
public sealed record RegisterStaffCommand(string Email, string Name, Guid HomeBranchId, Guid? InstitutionId, bool BootstrapSystemAdmin)
    : ICommand<StaffResponse>, IAuditableCommand
{
    public string AuditEntityId => StaffUser.NormalizeEmail(Email);
}

public sealed record RequestRoleGrantCommand(Guid StaffId, Role Role) : ICommand<ApprovalResponse>, IAuditableCommand
{
    public string AuditEntityId => StaffId.ToString();
}

public sealed record RemoveRoleCommand(Guid StaffId, Role Role) : ICommand<StaffResponse>, IAuditableCommand
{
    public string AuditEntityId => StaffId.ToString();
}

public sealed record ChangeApprovalLimitCommand(Guid StaffId, ApprovalLimitType LimitType, decimal MaxAmount)
    : ICommand<LimitChangeResponse>, IAuditableCommand
{
    public string AuditEntityId => StaffId.ToString();
}

public sealed record DeactivateStaffCommand(Guid StaffId) : ICommand<StaffResponse>, IAuditableCommand
{
    public string AuditEntityId => StaffId.ToString();
}

public sealed record ReactivateStaffCommand(Guid StaffId) : ICommand<StaffResponse>, IAuditableCommand
{
    public string AuditEntityId => StaffId.ToString();
}

internal sealed class RegisterStaffCommandValidator : AbstractValidator<RegisterStaffCommand>
{
    public RegisterStaffCommandValidator()
    {
        RuleFor(c => c.Email).NotEmpty().EmailAddress().MaximumLength(StaffUser.EmailMaxLength).WithMessage("A valid email address is required.");
        RuleFor(c => c.Name).Must(n => (n?.Trim().Length ?? 0) is >= StaffUser.NameMinLength and <= StaffUser.NameMaxLength)
            .WithMessage($"Name must be {StaffUser.NameMinLength}–{StaffUser.NameMaxLength} characters.");
        RuleFor(c => c.HomeBranchId).NotEmpty().WithMessage("A home branch is required.");
    }
}

internal sealed class ChangeApprovalLimitCommandValidator : AbstractValidator<ChangeApprovalLimitCommand>
{
    public ChangeApprovalLimitCommandValidator()
    {
        RuleFor(c => c.LimitType).IsInEnum().WithMessage("Unknown limit type.");
        RuleFor(c => c.MaxAmount).GreaterThanOrEqualTo(0).WithMessage("The limit must be zero or more.");
    }
}

/// <summary>Loads a staff user the caller may manage (same institution, or PlatformAdmin).</summary>
internal sealed class ManagedStaffLoader(IStaffRepository staff, ICurrentUser currentUser)
{
    public async Task<Result<StaffUser>> LoadAsync(Guid staffId, CancellationToken cancellationToken)
    {
        var user = await staff.GetAsync(new StaffUserId(staffId), cancellationToken).ConfigureAwait(false);
        // Another institution's staff is reported as not found rather than revealing it exists.
        if (user is null || (user.InstitutionId is { } institution && !currentUser.CanAccessInstitution(institution))
            || (user.InstitutionId is null && !currentUser.IsPlatformAdmin && currentUser.Kind != UserKind.System))
        {
            return IdentityErrors.StaffNotFound(staffId);
        }

        return user;
    }
}

internal sealed class RegisterStaffCommandHandler(
    IStaffRepository staff,
    IInstitutionDirectory institutions,
    IOrganizationModuleApi organization,
    IIdentityUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    ILogger<RegisterStaffCommandHandler> logger) : ICommandHandler<RegisterStaffCommand, StaffResponse>
{
    public async Task<Result<StaffResponse>> Handle(RegisterStaffCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var trusted = currentUser.IsPlatformAdmin || currentUser.Kind == UserKind.System;
        // Institution staff always register into their own institution; only the platform may choose another one.
        var institutionId = trusted ? command.InstitutionId ?? currentUser.InstitutionId : currentUser.InstitutionId;
        if (institutionId is null || (command.InstitutionId is { } requested && !currentUser.CanAccessInstitution(requested)))
        {
            return IdentityErrors.AccessDenied;
        }

        if (command.BootstrapSystemAdmin && !trusted)
        {
            return IdentityErrors.AccessDenied;
        }

        var institution = await institutions.GetAsync(institutionId.Value, cancellationToken).ConfigureAwait(false);
        if (institution is null || institution.IsClosed)
        {
            return IdentityErrors.StaffInactive;
        }

        var branch = await organization.GetBranchAsync(institutionId.Value, command.HomeBranchId, cancellationToken).ConfigureAwait(false);
        if (branch is not { Status: "Active" })
        {
            return IdentityErrors.InvalidHomeBranch;
        }

        if (await staff.GetByEmailAsync(StaffUser.NormalizeEmail(command.Email), cancellationToken).ConfigureAwait(false) is not null)
        {
            return IdentityErrors.StaffEmailTaken;
        }

        var registered = StaffUser.Register(command.Email, command.Name, institutionId.Value, command.HomeBranchId);
        if (registered.IsFailure)
        {
            return registered.Error;
        }

        var user = registered.Value;
        if (command.BootstrapSystemAdmin)
        {
            // Bootstrap exception (spec 002): only while the institution has no active SystemAdmin. Without it a
            // brand-new institution could never get an admin, because four-eyes needs a second person.
            if (await staff.HasActiveSystemAdminAsync(institutionId.Value, cancellationToken).ConfigureAwait(false))
            {
                return IdentityErrors.BootstrapNotAllowed;
            }

            var granted = user.GrantRole(Role.SystemAdmin, timeProvider.GetUtcNow().UtcDateTime);
            if (granted.IsFailure)
            {
                return granted.Error;
            }

            logger.LogWarning(
                "Bootstrap: {Actor} registered {Email} as first SystemAdmin of institution {InstitutionId} without approval",
                currentUser.Id, user.Email, institutionId);
        }

        staff.Add(user);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (UniqueConstraintViolationException)
        {
            return IdentityErrors.StaffEmailTaken;
        }

        return StaffMapping.ToResponse(user);
    }
}

internal sealed class RequestRoleGrantCommandHandler(
    ManagedStaffLoader loader,
    IApprovalRepository approvals,
    IIdentityUnitOfWork unitOfWork,
    ApprovalOperationCatalog catalog,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : ICommandHandler<RequestRoleGrantCommand, ApprovalResponse>
{
    public async Task<Result<ApprovalResponse>> Handle(RequestRoleGrantCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var loaded = await loader.LoadAsync(command.StaffId, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return loaded.Error;
        }

        var user = loaded.Value;
        // Reject obviously pointless requests up front; anything that changes later is re-checked at execution.
        if (user.Status == StaffStatus.Deactivated)
        {
            return IdentityErrors.StaffInactive;
        }

        if (user.HasRole(command.Role))
        {
            return IdentityErrors.RoleAlreadyGranted;
        }

        if (command.Role == Role.PlatformAdmin || user.InstitutionId is null)
        {
            return IdentityErrors.RoleNotAssignable;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var operation = catalog.Find(IdentityOperations.GrantRole)!;
        var request = ApprovalRequest.Create(
            user.InstitutionId, IdentityOperations.Module, IdentityOperations.GrantRole, user.Id.Value.ToString(), user.Id.Value,
            amount: null, currency: null,
            IdentityPayloads.Serialize(new GrantRolePayload(command.Role, user.Id.Value, user.Name, user.Email)),
            currentUser.StaffId ?? Guid.Empty, ApprovalOperationCatalog.RequiredCheckers(operation, null), now + catalog.Expiry);

        approvals.Add(request);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ApprovalMapping.ToResponse(request);
    }
}

/// <summary>Shared tail for commands that change a user's access immediately: commit, then clear cached access.</summary>
internal sealed class ImmediateAccessChange(IIdentityUnitOfWork unitOfWork, IAccessCache accessCache)
{
    public async Task<Result<StaffResponse>> CommitAsync(StaffUser user, Result change, CancellationToken cancellationToken)
    {
        if (change.IsFailure)
        {
            return change.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        // Spec 002: removing access is effective on the user's very next request.
        await accessCache.InvalidateStaffAsync(user.Id.Value, cancellationToken).ConfigureAwait(false);
        return StaffMapping.ToResponse(user);
    }
}

internal sealed class RemoveRoleCommandHandler(ManagedStaffLoader loader, ImmediateAccessChange commit, TimeProvider timeProvider)
    : ICommandHandler<RemoveRoleCommand, StaffResponse>
{
    public async Task<Result<StaffResponse>> Handle(RemoveRoleCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var loaded = await loader.LoadAsync(command.StaffId, cancellationToken).ConfigureAwait(false);
        return loaded.IsFailure
            ? loaded.Error
            : await commit.CommitAsync(loaded.Value, loaded.Value.RemoveRole(command.Role, timeProvider.GetUtcNow().UtcDateTime), cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class DeactivateStaffCommandHandler(ManagedStaffLoader loader, ImmediateAccessChange commit, TimeProvider timeProvider)
    : ICommandHandler<DeactivateStaffCommand, StaffResponse>
{
    public async Task<Result<StaffResponse>> Handle(DeactivateStaffCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var loaded = await loader.LoadAsync(command.StaffId, cancellationToken).ConfigureAwait(false);
        return loaded.IsFailure
            ? loaded.Error
            : await commit.CommitAsync(loaded.Value, loaded.Value.Deactivate(timeProvider.GetUtcNow().UtcDateTime), cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class ReactivateStaffCommandHandler(ManagedStaffLoader loader, ImmediateAccessChange commit, TimeProvider timeProvider)
    : ICommandHandler<ReactivateStaffCommand, StaffResponse>
{
    public async Task<Result<StaffResponse>> Handle(ReactivateStaffCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var loaded = await loader.LoadAsync(command.StaffId, cancellationToken).ConfigureAwait(false);
        return loaded.IsFailure
            ? loaded.Error
            : await commit.CommitAsync(loaded.Value, loaded.Value.Reactivate(timeProvider.GetUtcNow().UtcDateTime), cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class ChangeApprovalLimitCommandHandler(
    ManagedStaffLoader loader,
    ImmediateAccessChange commit,
    IApprovalRepository approvals,
    IInstitutionDirectory institutions,
    IIdentityUnitOfWork unitOfWork,
    ApprovalOperationCatalog catalog,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : ICommandHandler<ChangeApprovalLimitCommand, LimitChangeResponse>
{
    public async Task<Result<LimitChangeResponse>> Handle(ChangeApprovalLimitCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var loaded = await loader.LoadAsync(command.StaffId, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return loaded.Error;
        }

        var user = loaded.Value;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var current = user.ApprovalLimitFor(command.LimitType);

        // Lowering (or keeping) authority applies immediately — reducing access never waits (spec 002).
        if (command.MaxAmount <= current)
        {
            var applied = await commit.CommitAsync(user, user.SetApprovalLimit(command.LimitType, command.MaxAmount, now), cancellationToken).ConfigureAwait(false);
            return applied.IsFailure ? applied.Error : new LimitChangeResponse(applied.Value, null);
        }

        if (user.Status == StaffStatus.Deactivated || user.InstitutionId is null)
        {
            return IdentityErrors.StaffInactive;
        }

        // Limits are expressed in the institution's base currency (spec 002).
        var institution = await institutions.GetAsync(user.InstitutionId.Value, cancellationToken).ConfigureAwait(false);
        var operation = catalog.Find(IdentityOperations.RaiseApprovalLimit)!;
        var request = ApprovalRequest.Create(
            user.InstitutionId, IdentityOperations.Module, IdentityOperations.RaiseApprovalLimit, user.Id.Value.ToString(), user.Id.Value,
            command.MaxAmount, institution?.BaseCurrency,
            IdentityPayloads.Serialize(new RaiseLimitPayload(command.LimitType, current, command.MaxAmount, user.Id.Value, user.Name)),
            currentUser.StaffId ?? Guid.Empty, ApprovalOperationCatalog.RequiredCheckers(operation, command.MaxAmount), now + catalog.Expiry);

        approvals.Add(request);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new LimitChangeResponse(null, ApprovalMapping.ToResponse(request));
    }
}
