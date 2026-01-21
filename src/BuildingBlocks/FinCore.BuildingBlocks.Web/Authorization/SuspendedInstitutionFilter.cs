using FinCore.BuildingBlocks.Web.Security;
using Microsoft.AspNetCore.Http;

namespace FinCore.BuildingBlocks.Web.Authorization;

/// <summary>
/// Staff of a Suspended institution may read but not change anything (spec 002). Applied once to the whole module
/// route group, so every state-changing endpoint — including ones added by future modules — is covered without each
/// handler remembering to check.
/// </summary>
public sealed class SuspendedInstitutionFilter : IEndpointFilter
{
    public const string SuspendedStatus = "Suspended";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var http = context.HttpContext;
        if (!HttpMethods.IsGet(http.Request.Method)
            && !HttpMethods.IsHead(http.Request.Method)
            && string.Equals(http.User.FindFirst(FinCoreClaimTypes.InstitutionStatus)?.Value, SuspendedStatus, StringComparison.Ordinal))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Institution suspended",
                detail: "Your institution is suspended; data can be viewed but not changed.",
                extensions: new Dictionary<string, object?> { ["code"] = "Organization.InstitutionSuspended" });
        }

        return await next(context).ConfigureAwait(false);
    }
}
