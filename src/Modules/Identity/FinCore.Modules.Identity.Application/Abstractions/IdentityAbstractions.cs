using FinCore.Modules.Identity.Domain.Approvals;
using FinCore.Modules.Identity.Domain.Institutions;
using FinCore.Modules.Identity.Domain.Staff;

namespace FinCore.Modules.Identity.Application.Abstractions;

/// <summary>Write-side access to staff users.</summary>
public interface IStaffRepository
{
    Task<StaffUser?> GetAsync(StaffUserId id, CancellationToken cancellationToken);

    Task<StaffUser?> GetBySubjectAsync(string subject, CancellationToken cancellationToken);

    Task<StaffUser?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<bool> HasActiveSystemAdminAsync(Guid institutionId, CancellationToken cancellationToken);

    Task<bool> HasActiveStaffAtBranchAsync(Guid branchId, CancellationToken cancellationToken);

    void Add(StaffUser staffUser);
}

/// <summary>Write-side access to approval requests.</summary>
public interface IApprovalRepository
{
    Task<ApprovalRequest?> GetAsync(ApprovalRequestId id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ApprovalRequest>> ListPendingAsync(Guid? institutionId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ApprovalRequest>> ListDueForExpiryAsync(DateTime utcNow, CancellationToken cancellationToken);

    /// <summary>Requests created from another module's <c>ApprovalRequested</c> event, to make that handler idempotent.</summary>
    Task<bool> ExistsForEntityAsync(string module, string operationType, string entityId, CancellationToken cancellationToken);

    void Add(ApprovalRequest request);
}

/// <summary>
/// Institution facts Identity needs (status, base currency). Reads Identity's event-fed copy and falls back to
/// Organization's read API when an institution was created moments ago and its event has not arrived yet.
/// </summary>
public interface IInstitutionDirectory
{
    Task<InstitutionSnapshot?> GetAsync(Guid institutionId, CancellationToken cancellationToken);

    /// <summary>Records a new institution as Active; leaves an existing copy untouched (called on <c>InstitutionCreated</c>).</summary>
    Task AddIfMissingAsync(Guid institutionId, string code, string baseCurrency, DateTime occurredOnUtc, CancellationToken cancellationToken);

    /// <summary>Applies a status change unless the copy already reflects a later one (called on <c>InstitutionStatusChanged</c>).</summary>
    Task ApplyStatusAsync(Guid institutionId, string status, DateTime occurredOnUtc, CancellationToken cancellationToken);
}

/// <summary>
/// Cached access profiles used by the claims transformation. Access must change on the very next request after an
/// admin action (spec 002), so every access-changing handler invalidates explicitly after committing.
/// </summary>
public interface IAccessCache
{
    Task InvalidateStaffAsync(Guid staffId, CancellationToken cancellationToken);

    Task InvalidateInstitutionAsync(Guid institutionId, CancellationToken cancellationToken);
}

public interface IIdentityUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>Forgets unsaved changes after a lost race, so a later save in the same request does not retry them.</summary>
    void DiscardChanges();
}

/// <summary>Read side for staff listings (no-tracking projections).</summary>
public interface IIdentityQueries
{
    /// <param name="institutionId">Null lists every institution (PlatformAdmin only).</param>
    /// <param name="branchId">Branch scope or explicit filter; null for all branches.</param>
    Task<FinCore.BuildingBlocks.Application.Pagination.PagedResult<Staff.StaffResponse>> ListStaffAsync(
        Guid? institutionId, Guid? branchId, string? search, int page, int pageSize, CancellationToken cancellationToken);

    Task<IReadOnlyList<Domain.Approvals.ApprovalRequest>> ListByMakerAsync(Guid makerStaffId, CancellationToken cancellationToken);
}
