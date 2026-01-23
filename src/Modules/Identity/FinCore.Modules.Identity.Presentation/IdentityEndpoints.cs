using System.Security.Claims;
using FinCore.BuildingBlocks.Application.Messaging;
using FinCore.BuildingBlocks.Application.Pagination;
using FinCore.BuildingBlocks.Web;
using FinCore.BuildingBlocks.Web.Authorization;
using FinCore.BuildingBlocks.Web.Security;
using FinCore.Modules.Identity.Application.Approvals;
using FinCore.Modules.Identity.Application.Me;
using FinCore.Modules.Identity.Application.Staff;
using FinCore.Modules.Identity.Domain.Staff;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace FinCore.Modules.Identity.Presentation;

/// <summary>Request body for pre-registering a staff user (spec 002).</summary>
/// <param name="InstitutionId">PlatformAdmin only.</param>
/// <param name="BootstrapSystemAdmin">PlatformAdmin only: first SystemAdmin of an institution (bootstrap exception).</param>
public sealed record RegisterStaffRequest(string Email, string Name, Guid HomeBranchId, Guid? InstitutionId, bool BootstrapSystemAdmin = false);

/// <summary>Request body for a role grant; the grant itself happens only after approval.</summary>
public sealed record GrantRoleRequest(Role Role);

/// <summary>Request body for setting an approval limit (base currency of the institution).</summary>
public sealed record ApprovalLimitRequest(decimal MaxAmount);

/// <summary>Optional checker comment on approval.</summary>
public sealed record ApproveRequest(string? Comment);

/// <summary>Mandatory reason on rejection; shown to the maker.</summary>
public sealed record RejectRequest(string Reason);

/// <summary>Identity & Approvals endpoints (spec 002 API contract).</summary>
public static class IdentityEndpoints
{
    public const string OpenApiGroup = "identity";

    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/me", GetMeAsync)
            .WithTags("Me")
            .WithGroupName(OpenApiGroup)
            .WithName("GetMe")
            .WithSummary("The signed-in user: identity, institution, branch, roles and permissions")
            .RequireAuthorization()
            .Produces<MeResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        MapStaff(app.MapGroup("/staff").WithTags("Staff").WithGroupName(OpenApiGroup));
        MapApprovals(app.MapGroup("/approvals").WithTags("Approvals").WithGroupName(OpenApiGroup));
        return app;
    }

    private static void MapStaff(RouteGroupBuilder staff)
    {
        // Registering an institution's first SystemAdmin is the PlatformAdmin's job; everything else is identity.manage.
        staff.MapPost("/", RegisterAsync)
            .WithName("RegisterStaff")
            .WithSummary("Pre-register a staff user (no roles); linked on first sign-in by verified email")
            .RequireAuthorization(p => p.RequireAssertion(ctx =>
                ctx.User.HasClaim(Permissions.ClaimType, Permissions.Identity.Manage)
                || ctx.User.HasClaim(Permissions.ClaimType, Permissions.Platform.InstitutionsManage)))
            .Produces<StaffResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        staff.MapGet("/", async (IDispatcher d, CancellationToken ct, [FromQuery] string? search, [FromQuery] Guid? branchId,
                [FromQuery] int page = 1, [FromQuery] int pageSize = Paging.DefaultPageSize) =>
                ToResult(await d.Send(new ListStaffQuery(search, branchId, page, pageSize), ct).ConfigureAwait(false)))
            .WithName("ListStaff")
            .WithSummary("List staff (branch-scoped unless the caller has all-branch scope)")
            .RequirePermission(Permissions.Identity.Manage)
            .Produces<PagedResult<StaffResponse>>()
            .ProducesValidationProblem();

        staff.MapGet("/{id:guid}", async (Guid id, IDispatcher d, CancellationToken ct) =>
                ToResult(await d.Send(new GetStaffQuery(id), ct).ConfigureAwait(false)))
            .WithName("GetStaff")
            .WithSummary("Get a staff user with roles and limits")
            .RequirePermission(Permissions.Identity.Manage)
            .Produces<StaffResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        staff.MapPost("/{id:guid}/roles", async (Guid id, GrantRoleRequest request, IDispatcher d, CancellationToken ct) =>
            {
                var result = await d.Send(new RequestRoleGrantCommand(id, request.Role), ct).ConfigureAwait(false);
                // 202: nothing changed yet — a checker must approve first (spec 002).
                return result.IsSuccess ? TypedResults.Accepted($"/api/v1/approvals/{result.Value.Id}", result.Value) : result.ToProblem();
            })
            .WithName("RequestRoleGrant")
            .WithSummary("Request a role grant; applied only after a second admin approves")
            .RequirePermission(Permissions.Identity.Manage)
            .Produces<ApprovalResponse>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status409Conflict);

        staff.MapDelete("/{id:guid}/roles/{role}", async (Guid id, Role role, IDispatcher d, CancellationToken ct) =>
                ToResult(await d.Send(new RemoveRoleCommand(id, role), ct).ConfigureAwait(false)))
            .WithName("RemoveRole")
            .WithSummary("Remove a role immediately")
            .RequirePermission(Permissions.Identity.Manage)
            .Produces<StaffResponse>()
            .ProducesProblem(StatusCodes.Status409Conflict);

        staff.MapPut("/{id:guid}/approval-limits/{type}", async (Guid id, ApprovalLimitType type, ApprovalLimitRequest request, IDispatcher d, CancellationToken ct) =>
            {
                var result = await d.Send(new ChangeApprovalLimitCommand(id, type, request.MaxAmount), ct).ConfigureAwait(false);
                if (result.IsFailure)
                {
                    return result.ToProblem();
                }

                // Raising waits for approval (202); lowering is applied straight away (200).
                return result.Value.PendingApproval is { } pending
                    ? TypedResults.Accepted($"/api/v1/approvals/{pending.Id}", result.Value)
                    : TypedResults.Ok(result.Value);
            })
            .WithName("ChangeApprovalLimit")
            .WithSummary("Lower a limit now, or request a raise that needs approval")
            .RequirePermission(Permissions.Identity.Manage)
            .Produces<LimitChangeResponse>()
            .Produces<LimitChangeResponse>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem();

        staff.MapPost("/{id:guid}/deactivate", async (Guid id, IDispatcher d, CancellationToken ct) =>
                ToResult(await d.Send(new DeactivateStaffCommand(id), ct).ConfigureAwait(false)))
            .WithName("DeactivateStaff")
            .WithSummary("Deactivate a staff user immediately")
            .RequirePermission(Permissions.Identity.Manage)
            .Produces<StaffResponse>();

        staff.MapPost("/{id:guid}/reactivate", async (Guid id, IDispatcher d, CancellationToken ct) =>
                ToResult(await d.Send(new ReactivateStaffCommand(id), ct).ConfigureAwait(false)))
            .WithName("ReactivateStaff")
            .WithSummary("Reactivate a staff user with no roles")
            .RequirePermission(Permissions.Identity.Manage)
            .Produces<StaffResponse>();
    }

    private static void MapApprovals(RouteGroupBuilder approvals)
    {
        approvals.MapGet("/", async (IDispatcher d, CancellationToken ct, [FromQuery] string? status, [FromQuery] bool mine = false) =>
                ToResult(await d.Send(new ListApprovalsQuery(mine), ct).ConfigureAwait(false)))
            .WithName("ListApprovals")
            .WithSummary("Inbox of pending requests the caller may decide, or ?mine=true for the caller's own requests")
            .RequireStaff()
            .Produces<IReadOnlyList<ApprovalResponse>>();

        approvals.MapGet("/{id:guid}", async (Guid id, IDispatcher d, CancellationToken ct) =>
                ToResult(await d.Send(new GetApprovalQuery(id), ct).ConfigureAwait(false)))
            .WithName("GetApproval")
            .WithSummary("A request with its payload and decisions")
            .RequireStaff()
            .Produces<ApprovalResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        approvals.MapPost("/{id:guid}/approve", async (Guid id, ApproveRequest? body, IDispatcher d, CancellationToken ct) =>
                ToResult(await d.Send(new ApproveRequestCommand(id, body?.Comment), ct).ConfigureAwait(false)))
            .WithName("ApproveRequest")
            .WithSummary("Approve as checker (four-eyes, limits and checker count enforced)")
            .RequireStaff()
            .Produces<ApprovalResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        approvals.MapPost("/{id:guid}/reject", async (Guid id, RejectRequest body, IDispatcher d, CancellationToken ct) =>
                ToResult(await d.Send(new RejectRequestCommand(id, body.Reason), ct).ConfigureAwait(false)))
            .WithName("RejectRequest")
            .WithSummary("Reject as checker; a reason is required")
            .RequireStaff()
            .Produces<ApprovalResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        approvals.MapPost("/{id:guid}/withdraw", async (Guid id, IDispatcher d, CancellationToken ct) =>
                ToResult(await d.Send(new WithdrawRequestCommand(id), ct).ConfigureAwait(false)))
            .WithName("WithdrawRequest")
            .WithSummary("Withdraw your own pending request")
            .RequireStaff()
            .Produces<ApprovalResponse>()
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> GetMeAsync(ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken ct)
    {
        // The claims transformation records why a signed-in account has no access; report it with its stable code.
        if (user.FindFirst(FinCoreClaimTypes.AccessDenied)?.Value is { } code)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "No access to FinCore",
                detail: DescribeDenial(code),
                extensions: new Dictionary<string, object?> { ["code"] = code });
        }

        var result = await dispatcher.Send(new GetMeQuery(user.FindFirst("name")?.Value, user.FindFirst("email")?.Value), ct).ConfigureAwait(false);
        return ToResult(result);
    }

    private static string DescribeDenial(string code) => code switch
    {
        "Identity.EmailNotVerified" => "Verify your email address with the identity provider before signing in.",
        "Identity.StaffInactive" => "This staff account or its institution is no longer active.",
        _ => "This account is not registered as FinCore staff. Contact your administrator.",
    };

    private static async Task<IResult> RegisterAsync(RegisterStaffRequest request, IDispatcher dispatcher, HttpContext http, CancellationToken ct)
    {
        var result = await dispatcher.Send(
            new RegisterStaffCommand(request.Email, request.Name, request.HomeBranchId, request.InstitutionId, request.BootstrapSystemAdmin), ct)
            .ConfigureAwait(false);
        return result.IsSuccess
            ? TypedResults.Created($"{http.Request.PathBase}/api/v1/staff/{result.Value.Id}", result.Value)
            : result.ToProblem();
    }

    private static IResult ToResult<T>(FinCore.SharedKernel.Results.Result<T> result) =>
        result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblem();
}
