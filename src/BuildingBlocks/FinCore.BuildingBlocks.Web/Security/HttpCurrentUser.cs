using System.Security.Claims;
using FinCore.BuildingBlocks.Application.Security;
using FinCore.BuildingBlocks.Web.Authorization;
using Microsoft.AspNetCore.Http;

namespace FinCore.BuildingBlocks.Web.Security;

/// <summary>Current user from the request's claims principal; <c>system</c> when there is no request or user.</summary>
internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? User => accessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    public string Id =>
        IsAuthenticated
            ? User!.FindFirstValue("sub") ?? User!.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown"
            : accessor.HttpContext is null ? "system" : "anonymous";

    public bool HasPermission(string permission) => User?.HasClaim(Permissions.ClaimType, permission) == true;
}
