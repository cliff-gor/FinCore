using FinCore.BuildingBlocks.Application.Pagination;
using FinCore.BuildingBlocks.Application.Persistence;
using FinCore.Modules.Identity.Application.Abstractions;
using FinCore.Modules.Identity.Application.Staff;
using FinCore.Modules.Identity.Domain.Approvals;
using FinCore.Modules.Identity.Domain.Institutions;
using FinCore.Modules.Identity.Domain.Staff;
using FinCore.Modules.Organization.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FinCore.Modules.Identity.Infrastructure.Persistence.Repositories;

internal sealed class StaffRepository(IdentityDbContext dbContext) : IStaffRepository
{
    public Task<StaffUser?> GetAsync(StaffUserId id, CancellationToken cancellationToken) =>
        dbContext.StaffUsers.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<StaffUser?> GetBySubjectAsync(string subject, CancellationToken cancellationToken) =>
        dbContext.StaffUsers.FirstOrDefaultAsync(s => s.ExternalSubjectId == subject, cancellationToken);

    public Task<StaffUser?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        dbContext.StaffUsers.FirstOrDefaultAsync(s => s.Email == normalizedEmail, cancellationToken);

    public Task<bool> HasActiveSystemAdminAsync(Guid institutionId, CancellationToken cancellationToken) =>
        dbContext.StaffUsers.AnyAsync(
            s => s.InstitutionId == institutionId && s.Status != StaffStatus.Deactivated && s.Roles.Any(r => r.Role == Role.SystemAdmin),
            cancellationToken);

    public Task<bool> HasActiveStaffAtBranchAsync(Guid branchId, CancellationToken cancellationToken) =>
        dbContext.StaffUsers.AnyAsync(s => s.HomeBranchId == branchId && s.Status != StaffStatus.Deactivated, cancellationToken);

    public void Add(StaffUser staffUser) => dbContext.StaffUsers.Add(staffUser);
}

internal sealed class ApprovalRepository(IdentityDbContext dbContext) : IApprovalRepository
{
    public Task<ApprovalRequest?> GetAsync(ApprovalRequestId id, CancellationToken cancellationToken) =>
        dbContext.ApprovalRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ApprovalRequest>> ListPendingAsync(Guid? institutionId, CancellationToken cancellationToken) =>
        await dbContext.ApprovalRequests
            .Where(r => r.Status == ApprovalStatus.Pending && (institutionId == null || r.InstitutionId == institutionId))
            .OrderBy(r => r.ExpiresAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<ApprovalRequest>> ListDueForExpiryAsync(DateTime utcNow, CancellationToken cancellationToken) =>
        await dbContext.ApprovalRequests
            .Where(r => r.Status == ApprovalStatus.Pending && r.ExpiresAtUtc <= utcNow)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<bool> ExistsForEntityAsync(string module, string operationType, string entityId, CancellationToken cancellationToken) =>
        dbContext.ApprovalRequests.AnyAsync(r => r.Module == module && r.OperationType == operationType && r.EntityId == entityId, cancellationToken);

    public void Add(ApprovalRequest request) => dbContext.ApprovalRequests.Add(request);
}

internal sealed class IdentityQueries(IdentityDbContext dbContext) : IIdentityQueries
{
    public async Task<PagedResult<StaffResponse>> ListStaffAsync(
        Guid? institutionId, Guid? branchId, string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.StaffUsers.AsNoTracking();
        if (institutionId is { } institution)
        {
            query = query.Where(s => s.InstitutionId == institution);
        }

        if (branchId is { } branch)
        {
            query = query.Where(s => s.HomeBranchId == branch);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            // ILike with an escaped pattern: user input never becomes part of the SQL text (spec §28).
            var pattern = $"%{search.Trim().Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal)}%";
            query = query.Where(s => EF.Functions.ILike(s.Name, pattern) || EF.Functions.ILike(s.Email, pattern));
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query.OrderBy(s => s.Name).ThenBy(s => s.Email).Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return new PagedResult<StaffResponse>(items.Select(StaffMapping.ToResponse).ToList(), page, pageSize, total);
    }

    public async Task<IReadOnlyList<ApprovalRequest>> ListByMakerAsync(Guid makerStaffId, CancellationToken cancellationToken) =>
        await dbContext.ApprovalRequests.AsNoTracking()
            .Where(r => r.MakerStaffId == makerStaffId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}

internal sealed class InstitutionDirectory(IdentityDbContext dbContext, IOrganizationModuleApi organization) : IInstitutionDirectory
{
    public async Task<InstitutionSnapshot?> GetAsync(Guid institutionId, CancellationToken cancellationToken)
    {
        var snapshot = await FindAsync(institutionId, cancellationToken).ConfigureAwait(false);
        if (snapshot is not null)
        {
            return snapshot;
        }

        // The InstitutionCreated event may not have arrived yet (outbox polls every 2 s). A synchronous read through
        // Organization's contract fills the gap once; afterwards the events keep the copy current. The read is stamped
        // as "unknown" time so that any status event, even one that committed while this read ran, still applies.
        var org = await organization.GetInstitutionAsync(institutionId, cancellationToken).ConfigureAwait(false);
        if (org is null)
        {
            return null;
        }

        return await InsertOrLoadAsync(
            new InstitutionSnapshot(org.Id, org.Code, org.Status, org.BaseCurrency, InstitutionSnapshot.UnknownAsOfUtc),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task AddIfMissingAsync(Guid institutionId, string code, string baseCurrency, DateTime occurredOnUtc, CancellationToken cancellationToken)
    {
        // Never overwrite: the copy may already hold a later status (filled by a read or a status event that won the race).
        if (await FindAsync(institutionId, cancellationToken).ConfigureAwait(false) is null)
        {
            await InsertOrLoadAsync(new InstitutionSnapshot(institutionId, code, "Active", baseCurrency, occurredOnUtc), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public async Task ApplyStatusAsync(Guid institutionId, string status, DateTime occurredOnUtc, CancellationToken cancellationToken)
    {
        var snapshot = await GetAsync(institutionId, cancellationToken).ConfigureAwait(false);
        if (snapshot is null || !snapshot.ApplyStatus(status, occurredOnUtc))
        {
            return;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private Task<InstitutionSnapshot?> FindAsync(Guid institutionId, CancellationToken cancellationToken) =>
        dbContext.Institutions.FirstOrDefaultAsync(i => i.Id == institutionId, cancellationToken);

    private async Task<InstitutionSnapshot> InsertOrLoadAsync(InstitutionSnapshot snapshot, CancellationToken cancellationToken)
    {
        dbContext.Institutions.Add(snapshot);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return snapshot;
        }
        catch (UniqueConstraintViolationException)
        {
            // An event handler and a request's fallback read raced to insert the same institution. Keep the winner's
            // row: callers that carry a status change apply it on top, ordered by event time.
            dbContext.ChangeTracker.Clear();
            return await dbContext.Institutions.FirstAsync(i => i.Id == snapshot.Id, cancellationToken).ConfigureAwait(false);
        }
    }
}

internal sealed class IdentityUnitOfWork(IdentityDbContext dbContext) : IIdentityUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);

    public void DiscardChanges() => dbContext.ChangeTracker.Clear();
}
