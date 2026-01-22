using System.Security.Cryptography;
using System.Text.Json;
using FinCore.BuildingBlocks.Application.Audit;
using FinCore.BuildingBlocks.Application.Messaging;
using FinCore.BuildingBlocks.Application.Security;
using FinCore.SharedKernel.Results;

namespace FinCore.BuildingBlocks.Application.Behaviors;

/// <summary>
/// Writes one audit row per successful command (spec §24, spec 002). Queries and failed commands are not audited:
/// failures are already in the logs with their error code, and only completed changes need a permanent trail.
/// </summary>
internal sealed class AuditBehavior<TRequest, TResponse>(
    IAuditLog auditLog,
    ICurrentUser currentUser,
    IRequestContext requestContext) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        var response = await next().ConfigureAwait(false);
        if (request is not ICommandBase || response.IsFailure)
        {
            return response;
        }

        // Hash, never store, the request: the trail proves what was sent without keeping personal data.
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request, request.GetType())));
        var entityId = request is IAuditableCommand auditable ? auditable.AuditEntityId : null;

        await auditLog.RecordAsync(
            new AuditRecord(currentUser.Id, currentUser.InstitutionId, typeof(TRequest).Name, entityId, requestContext.CorrelationId, hash),
            cancellationToken).ConfigureAwait(false);

        return response;
    }
}
