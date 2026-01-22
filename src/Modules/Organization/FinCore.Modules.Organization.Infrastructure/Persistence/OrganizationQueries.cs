using FinCore.BuildingBlocks.Application.Pagination;
using FinCore.Modules.Organization.Application.Abstractions;
using FinCore.Modules.Organization.Application.Branches;
using FinCore.Modules.Organization.Application.Institutions;
using FinCore.Modules.Organization.Domain.Branches;
using FinCore.Modules.Organization.Domain.Institutions;
using Microsoft.EntityFrameworkCore;

namespace FinCore.Modules.Organization.Infrastructure.Persistence;

/// <summary>
/// Read side of the module: no-tracking queries projected straight into response DTOs, so reads never load
/// aggregates or their domain events (spec §6, lightweight CQRS).
/// </summary>
internal sealed class OrganizationQueries(OrganizationDbContext dbContext) : IOrganizationQueries
{
    public Task<InstitutionResponse?> GetInstitutionAsync(Guid id, CancellationToken cancellationToken) =>
        Project(dbContext.Institutions.AsNoTracking().Where(i => i.Id == new InstitutionId(id)))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<PagedResult<InstitutionResponse>> ListInstitutionsAsync(Guid? onlyInstitutionId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.Institutions.AsNoTracking();
        if (onlyInstitutionId is { } only)
        {
            query = query.Where(i => i.Id == new InstitutionId(only));
        }

        // Order by code so paging is stable: without a deterministic order, rows can repeat or vanish between pages.
        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await Project(query.OrderBy(i => i.Code).Skip((page - 1) * pageSize).Take(pageSize))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<InstitutionResponse>(items, page, pageSize, total);
    }

    public Task<DateOnly?> GetBusinessDateAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Institutions.AsNoTracking()
            .Where(i => i.Id == new InstitutionId(id))
            .Select(i => (DateOnly?)i.CurrentBusinessDate)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<BranchResponse?> GetBranchAsync(Guid institutionId, Guid branchId, CancellationToken cancellationToken) =>
        ProjectBranches(dbContext.Branches.AsNoTracking()
                .Where(b => b.Id == new BranchId(branchId) && b.InstitutionId == new InstitutionId(institutionId)))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<PagedResult<BranchResponse>> ListBranchesAsync(
        Guid institutionId, Guid? onlyBranchId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.Branches.AsNoTracking().Where(b => b.InstitutionId == new InstitutionId(institutionId));
        if (onlyBranchId is { } only)
        {
            query = query.Where(b => b.Id == new BranchId(only));
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await ProjectBranches(query.OrderBy(b => b.Code).Skip((page - 1) * pageSize).Take(pageSize))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return new PagedResult<BranchResponse>(items, page, pageSize, total);
    }

    private static IQueryable<BranchResponse> ProjectBranches(IQueryable<Branch> query) =>
        query.Select(b => new BranchResponse(
            b.Id.Value,
            b.InstitutionId.Value,
            b.Code.Value,
            b.Name,
            b.Address,
            b.ParentBranchId == null ? null : b.ParentBranchId.Value.Value,
            b.Status.ToString(),
            b.CreatedAtUtc));

    private static IQueryable<InstitutionResponse> Project(IQueryable<Institution> query) =>
        query.Select(i => new InstitutionResponse(
            i.Id.Value,
            i.Code.Value,
            i.Name,
            i.Type.ToString(),
            i.BaseCurrency,
            i.LicenseNumber,
            i.Status.ToString(),
            i.CurrentBusinessDate,
            i.CreatedAtUtc));
}
