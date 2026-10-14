using FinCore.BuildingBlocks.Application.Messaging;
using FinCore.BuildingBlocks.Application.Pagination;
using FinCore.BuildingBlocks.Web;
using FinCore.BuildingBlocks.Web.Authorization;
using FinCore.Modules.Organization.Application.Institutions;
using FinCore.Modules.Organization.Application.Institutions.ChangeInstitutionStatus;
using FinCore.Modules.Organization.Application.Institutions.CreateInstitution;
using FinCore.Modules.Organization.Application.Institutions.GetBusinessDate;
using FinCore.Modules.Organization.Application.Institutions.GetInstitution;
using FinCore.Modules.Organization.Application.Institutions.ListInstitutions;
using FinCore.Modules.Organization.Domain.Institutions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace FinCore.Modules.Organization.Presentation.Institutions;

/// <summary>
/// HTTP request body for creating an institution. Kept separate from the command so the wire format can change
/// without touching application code.
/// </summary>
public sealed record CreateInstitutionRequest(
    string Code,
    string Name,
    InstitutionType Type,
    string BaseCurrency,
    string? LicenseNumber,
    DateOnly InitialBusinessDate);

/// <summary>Minimal API endpoints for institutions (spec §12). Mapped under <c>/api/v1</c> by the module registry.</summary>
public static class InstitutionEndpoints
{
    /// <summary>OpenAPI document name; the build exports it to <c>openapi/organization.json</c> for the front ends.</summary>
    public const string OpenApiGroup = "organization";

    public static IEndpointRouteBuilder MapInstitutionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/institutions")
            .WithTags("Institutions")
            .WithGroupName(OpenApiGroup);

        group.MapPost("/", CreateAsync)
            .WithName("CreateInstitution")
            .WithSummary("Create a Bank or MFI institution")
            // PlatformAdmin only (spec 002): institution admins manage their own institution, never create new ones.
            .RequirePermission(Permissions.Platform.InstitutionsManage)
            .Produces<InstitutionResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/", ListAsync)
            .WithName("ListInstitutions")
            .WithSummary("List institutions (paged, ordered by code)")
            .RequirePermission(Permissions.Organization.Read)
            .Produces<PagedResult<InstitutionResponse>>()
            .ProducesValidationProblem();

        group.MapGet("/{id:guid}", GetAsync)
            .WithName("GetInstitution")
            .WithSummary("Get an institution")
            .RequirePermission(Permissions.Organization.Read)
            .Produces<InstitutionResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/suspend", (Guid id, IDispatcher d, CancellationToken ct) => ChangeStatusAsync(id, InstitutionStatusAction.Suspend, d, ct))
            .WithName("SuspendInstitution")
            .WithSummary("Suspend an institution: its staff can read but not change data")
            .RequirePermission(Permissions.Platform.InstitutionsManage)
            .Produces<InstitutionResponse>()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/reactivate", (Guid id, IDispatcher d, CancellationToken ct) => ChangeStatusAsync(id, InstitutionStatusAction.Reactivate, d, ct))
            .WithName("ReactivateInstitution")
            .WithSummary("Lift an institution's suspension")
            .RequirePermission(Permissions.Platform.InstitutionsManage)
            .Produces<InstitutionResponse>()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/close", (Guid id, IDispatcher d, CancellationToken ct) => ChangeStatusAsync(id, InstitutionStatusAction.Close, d, ct))
            .WithName("CloseInstitution")
            .WithSummary("Close an institution for good (all branches must be closed)")
            .RequirePermission(Permissions.Platform.InstitutionsManage)
            .Produces<InstitutionResponse>()
            .ProducesProblem(StatusCodes.Status409Conflict);

        // Any signed-in staff member may read their institution's business date (spec §12): every screen shows it.
        group.MapGet("/{id:guid}/business-date", GetBusinessDateAsync)
            .WithName("GetInstitutionBusinessDate")
            .WithSummary("Get an institution's current business date")
            .RequireStaff()
            .Produces<BusinessDateResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> CreateAsync(
        CreateInstitutionRequest request, IDispatcher dispatcher, HttpContext http, CancellationToken ct)
    {
        var result = await dispatcher.Send(
            new CreateInstitutionCommand(
                request.Code, request.Name, request.Type, request.BaseCurrency, request.LicenseNumber, request.InitialBusinessDate),
            ct).ConfigureAwait(false);

        // Location points at the new resource, so clients can follow it instead of building URLs themselves.
        return result.IsSuccess
            ? TypedResults.Created($"{http.Request.PathBase}{http.Request.Path.Value?.TrimEnd('/')}/{result.Value.Id}", result.Value)
            : result.ToProblem();
    }

    private static async Task<IResult> ListAsync(
        IDispatcher dispatcher, CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = Paging.DefaultPageSize)
    {
        var result = await dispatcher.Send(new ListInstitutionsQuery(page, pageSize), ct).ConfigureAwait(false);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblem();
    }

    private static async Task<IResult> GetAsync(
        Guid id, IDispatcher dispatcher, CancellationToken ct)
    {
        var result = await dispatcher.Send(new GetInstitutionQuery(id), ct).ConfigureAwait(false);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblem();
    }

    private static async Task<IResult> ChangeStatusAsync(Guid id, InstitutionStatusAction action, IDispatcher dispatcher, CancellationToken ct)
    {
        var result = await dispatcher.Send(new ChangeInstitutionStatusCommand(id, action), ct).ConfigureAwait(false);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblem();
    }

    private static async Task<IResult> GetBusinessDateAsync(
        Guid id, IDispatcher dispatcher, CancellationToken ct)
    {
        var result = await dispatcher.Send(new GetBusinessDateQuery(id), ct).ConfigureAwait(false);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblem();
    }
}
