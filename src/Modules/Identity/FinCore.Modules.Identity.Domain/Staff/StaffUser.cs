using System.Net.Mail;
using FinCore.Modules.Identity.Domain.Errors;
using FinCore.SharedKernel.Domain;
using FinCore.SharedKernel.Results;

namespace FinCore.Modules.Identity.Domain.Staff;

/// <summary>
/// A staff member's authorization profile (master §13, spec 002). Authentication is the identity provider's job;
/// this aggregate only decides what an authenticated person may do in FinCore.
/// </summary>
/// <remarks>
/// Granting roles and raising limits is not exposed directly to admins: those go through maker-checker, and only the
/// approval executor calls <see cref="GrantRole"/> / <see cref="SetApprovalLimit"/> for increases.
/// </remarks>
public sealed class StaffUser : AggregateRoot<StaffUserId>
{
    public const int NameMinLength = 2;
    public const int NameMaxLength = 100;
    public const int EmailMaxLength = 254;

    private readonly List<StaffRole> _roles = [];
    private readonly List<ApprovalLimit> _approvalLimits = [];

    private StaffUser(StaffUserId id, string email, string name, Guid? institutionId, Guid? homeBranchId)
        : base(id)
    {
        Email = email;
        Name = name;
        InstitutionId = institutionId;
        HomeBranchId = homeBranchId;
        Status = StaffStatus.PendingFirstLogin;
    }

    /// <summary>EF Core materialization only.</summary>
    private StaffUser()
    {
        Email = null!;
        Name = null!;
    }

    /// <summary>Lower-cased; unique platform-wide, because first sign-in links the IdP account by email (spec 002).</summary>
    public string Email { get; private set; }

    public string Name { get; private set; }

    /// <summary>The IdP <c>sub</c>. Once set it is the key — later email changes at the IdP never relink the account.</summary>
    public string? ExternalSubjectId { get; private set; }

    /// <summary>Null only for PlatformAdmin, the single role not tied to an institution.</summary>
    public Guid? InstitutionId { get; private set; }

    public Guid? HomeBranchId { get; private set; }

    public StaffStatus Status { get; private set; }

    public IReadOnlyCollection<StaffRole> Roles => _roles.AsReadOnly();

    public IReadOnlyCollection<ApprovalLimit> ApprovalLimits => _approvalLimits.AsReadOnly();

    public bool IsActive => Status == StaffStatus.Active;

    public bool IsPlatformAdmin => HasRole(Role.PlatformAdmin);

    public IReadOnlyCollection<string> Permissions =>
        Status == StaffStatus.Deactivated ? [] : RolePermissions.For(_roles.Select(r => r.Role));

    public static string NormalizeEmail(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>
    /// Pre-registers an institution staff member with no roles (spec 002). Roles are then granted through approval,
    /// so registering someone never grants access by itself.
    /// </summary>
    public static Result<StaffUser> Register(string? email, string? name, Guid institutionId, Guid homeBranchId) =>
        Create(email, name, institutionId, homeBranchId);

    /// <summary>A platform administrator (seeded; not tied to an institution or branch).</summary>
    public static Result<StaffUser> RegisterPlatformAdmin(string? email, string? name, DateTime utcNow)
    {
        var created = Create(email, name, institutionId: null, homeBranchId: null);
        if (created.IsFailure)
        {
            return created;
        }

        created.Value.AddRole(Role.PlatformAdmin, utcNow);
        return created;
    }

    public bool HasRole(Role role) => _roles.Any(r => r.Role == role);

    public decimal ApprovalLimitFor(ApprovalLimitType type) =>
        _approvalLimits.FirstOrDefault(l => l.Type == type)?.MaxAmount ?? 0m;

    /// <summary>Links the identity provider account on first sign-in and activates the user.</summary>
    public Result LinkIdentity(string subject, DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        if (ExternalSubjectId is not null)
        {
            return string.Equals(ExternalSubjectId, subject, StringComparison.Ordinal)
                ? Result.Success()
                : IdentityErrors.StaffAlreadyLinked;
        }

        if (Status == StaffStatus.Deactivated)
        {
            return IdentityErrors.StaffInactive;
        }

        ExternalSubjectId = subject;
        Status = StaffStatus.Active;
        AccessChanged("linked", utcNow);
        return Result.Success();
    }

    /// <summary>Adds a role. Called by the approval executor, or by the audited bootstrap exception (spec 002).</summary>
    public Result GrantRole(Role role, DateTime utcNow)
    {
        if (Status == StaffStatus.Deactivated)
        {
            return IdentityErrors.StaffInactive;
        }

        if (HasRole(role))
        {
            return IdentityErrors.RoleAlreadyGranted;
        }

        // PlatformAdmin is never granted at runtime; platform administrators are provisioned by operations.
        if (role == Role.PlatformAdmin || InstitutionId is null)
        {
            return IdentityErrors.RoleNotAssignable;
        }

        AddRole(role, utcNow);
        AccessChanged($"role-granted:{role}", utcNow);
        return Result.Success();
    }

    /// <summary>Removes a role immediately: reducing access never waits for approval (spec 002).</summary>
    public Result RemoveRole(Role role, DateTime utcNow)
    {
        var assignment = _roles.FirstOrDefault(r => r.Role == role);
        if (assignment is null)
        {
            return IdentityErrors.RoleNotGranted;
        }

        _roles.Remove(assignment);
        AccessChanged($"role-removed:{role}", utcNow);
        return Result.Success();
    }

    /// <summary>
    /// Sets an approval limit. Lowering applies immediately; raising must come from an approved request — the caller
    /// (application layer) enforces which path is taken.
    /// </summary>
    public Result SetApprovalLimit(ApprovalLimitType type, decimal maxAmount, DateTime utcNow)
    {
        if (maxAmount < 0 || !Enum.IsDefined(type))
        {
            return IdentityErrors.InvalidApprovalLimit;
        }

        if (Status == StaffStatus.Deactivated)
        {
            return IdentityErrors.StaffInactive;
        }

        _approvalLimits.RemoveAll(l => l.Type == type);
        if (maxAmount > 0)
        {
            _approvalLimits.Add(new ApprovalLimit(type, maxAmount));
        }

        AccessChanged($"limit:{type}", utcNow);
        return Result.Success();
    }

    /// <summary>Deactivation is immediate and ends every session on the next request (permission cache cleared).</summary>
    public Result Deactivate(DateTime utcNow)
    {
        if (Status == StaffStatus.Deactivated)
        {
            return IdentityErrors.StaffInactive;
        }

        Status = StaffStatus.Deactivated;
        AccessChanged("deactivated", utcNow);
        return Result.Success();
    }

    /// <summary>
    /// Reactivates with <b>no roles</b> (spec 002): old privileges are never restored silently; they must be granted
    /// again through approval.
    /// </summary>
    public Result Reactivate(DateTime utcNow)
    {
        if (Status != StaffStatus.Deactivated)
        {
            return IdentityErrors.StaffNotDeactivated;
        }

        _roles.Clear();
        Status = ExternalSubjectId is null ? StaffStatus.PendingFirstLogin : StaffStatus.Active;
        AccessChanged("reactivated", utcNow);
        return Result.Success();
    }

    private static Result<StaffUser> Create(string? email, string? name, Guid? institutionId, Guid? homeBranchId)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (normalizedEmail.Length is 0 or > EmailMaxLength || !MailAddress.TryCreate(normalizedEmail, out _))
        {
            return IdentityErrors.InvalidEmail;
        }

        var trimmedName = name?.Trim() ?? string.Empty;
        if (trimmedName.Length is < NameMinLength or > NameMaxLength)
        {
            return IdentityErrors.InvalidName;
        }

        return new StaffUser(StaffUserId.New(), normalizedEmail, trimmedName, institutionId, homeBranchId);
    }

    private void AddRole(Role role, DateTime utcNow) => _roles.Add(new StaffRole(role, utcNow));

    // One event type for every access change: Identity uses it to clear cached permissions, later Notifications.
    private void AccessChanged(string change, DateTime utcNow) =>
        Raise(new StaffAccessChangedDomainEvent(Guid.CreateVersion7(), utcNow, Id, InstitutionId, change));
}

/// <summary>A role held by a staff user and when it was granted.</summary>
public sealed record StaffRole(Role Role, DateTime GrantedAtUtc) : IValueObject;

/// <summary>Maximum amount a checker may approve for one kind of operation, in the institution's base currency.</summary>
public sealed record ApprovalLimit(ApprovalLimitType Type, decimal MaxAmount) : IValueObject;

public sealed record StaffAccessChangedDomainEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    StaffUserId StaffId,
    Guid? InstitutionId,
    string Change) : DomainEvent(EventId, OccurredOnUtc);
