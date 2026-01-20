namespace FinCore.BuildingBlocks.Application.Pagination;

/// <summary>Standard paging envelope (spec §28): <c>{ items, page, pageSize, totalCount }</c>.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public static class Paging
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;
}
