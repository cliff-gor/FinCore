using FinCore.BuildingBlocks.Application.Messaging;
using FinCore.BuildingBlocks.Application.Pagination;
using FinCore.BuildingBlocks.Web;
using FinCore.BuildingBlocks.Web.Authorization;
using FinCore.Modules.Organization.Application.Branches;
using FinCore.Modules.Organization.Presentation.Institutions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace FinCore.Modules.Organization.Presentation.Branches;

/// <summary>Request body for creating a branch. The code is fixed once created; it cannot be updated.</summary>
public sealed record CreateBranchRequest(string Code, string Name, string? Address, Guid? ParentBranchId);

/// <summary>Request body for updating a branch's name, address and parent.</summary>
public sealed record UpdateBranchRequest(string Name, string? Address, Guid? ParentBranchId);

/// <summary>Branch endpoints, nested under their institution (spec 002).</summary>
public static class BranchEndpoints
{
    public static IEndpointRouteBuilder MapBranchEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/institutions/{institutionId:guid}/branches")
            .WithTags("Branches")
            .WithGroupName(InstitutionEndpoints.OpenApiGroup);

        group.MapPost("/", CreateAsync)
            .WithName("CreateBranch")
            .WithSummary("Open a branch")
            .RequirePermission(Permissions.Organization.Manage)
            .Produces<BranchResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/", ListAsync)
            .WithName("ListBranches")
            .WithSummary("List branches (only the home branch without all-branch scope)")
            .RequirePermission(Permissions.Organization.Read)
            .Produces<PagedResult<BranchResponse>>()
            .ProducesValidationProblem();

        group.MapGet("/{branchId:guid}", GetAsync)
            .WithName("GetBranch")
            .WithSummary("Get a branch")
            .RequirePermission(Permissions.Organization.Read)
            .Produces<BranchResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{branchId:guid}", UpdateAsync)
            .WithName("UpdateBranch")
            .WithSummary("Change a branch's name, address or parent")
            .RequirePermission(Permissions.Organization.Manage)
            .Produces<BranchResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{branchId:guid}/close", CloseAsync)
            .WithName("CloseBranch")
            .WithSummary("Close a branch (no active staff or child branches may remain)")
            .RequirePermission(Permissions.Organization.Manage)
            .Produces<BranchResponse>()
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<IResult> CreateAsync(Guid institutionId, CreateBranchRequest request, IDispatcher dispatcher, HttpContext http, CancellationToken ct)
    {
        var result = await dispatcher.Send(
            new CreateBranchCommand(institutionId, request.Code, request.Name, request.Address, request.ParentBranchId), ct).ConfigureAwait(false);
        return result.IsSuccess
            ? TypedResults.Created($"{http.Request.PathBase}{http.Request.Path.Value?.TrimEnd('/')}/{result.Value.Id}", result.Value)
            : result.ToProblem();
    }

    private static async Task<IResult> ListAsync(
        Guid institutionId, IDispatcher dispatcher, CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = Paging.DefaultPageSize)
    {
        var result = await dispatcher.Send(new ListBranchesQuery(institutionId, page, pageSize), ct).ConfigureAwait(false);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblem();
    }

    private static async Task<IResult> GetAsync(Guid institutionId, Guid branchId, IDispatcher dispatcher, CancellationToken ct)
    {
        var result = await dispatcher.Send(new GetBranchQuery(institutionId, branchId), ct).ConfigureAwait(false);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblem();
    }

    private static async Task<IResult> UpdateAsync(Guid institutionId, Guid branchId, UpdateBranchRequest request, IDispatcher dispatcher, CancellationToken ct)
    {
        var result = await dispatcher.Send(
            new UpdateBranchCommand(institutionId, branchId, request.Name, request.Address, request.ParentBranchId), ct).ConfigureAwait(false);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblem();
    }

    private static async Task<IResult> CloseAsync(Guid institutionId, Guid branchId, IDispatcher dispatcher, CancellationToken ct)
    {
        var result = await dispatcher.Send(new CloseBranchCommand(institutionId, branchId), ct).ConfigureAwait(false);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblem();
    }
}
