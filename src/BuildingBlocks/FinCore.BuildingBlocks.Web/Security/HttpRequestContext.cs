using FinCore.BuildingBlocks.Application.Security;
using Microsoft.AspNetCore.Http;

namespace FinCore.BuildingBlocks.Web.Security;

/// <summary>Reads per-request values that middleware stored in <see cref="HttpContext.Items"/>.</summary>
public sealed class HttpRequestContext(IHttpContextAccessor accessor) : IRequestContext
{
    /// <summary>Key under which the correlation middleware stores the request's correlation id.</summary>
    public const string CorrelationIdItem = "FinCore.CorrelationId";

    public string? CorrelationId => accessor.HttpContext?.Items[CorrelationIdItem] as string;
}
