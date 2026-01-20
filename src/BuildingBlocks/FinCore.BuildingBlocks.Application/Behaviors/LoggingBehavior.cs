using System.Diagnostics;
using FinCore.BuildingBlocks.Application.Messaging;
using FinCore.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace FinCore.BuildingBlocks.Application.Behaviors;

/// <summary>Logs request name, outcome and duration. Never logs request payloads (they may contain PII).</summary>
internal sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        var name = typeof(TRequest).Name;
        var started = Stopwatch.GetTimestamp();
        var response = await next().ConfigureAwait(false);
        var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;

        if (response.IsSuccess)
        {
            logger.LogInformation("Handled {RequestName} in {ElapsedMs:0.0} ms", name, elapsedMs);
        }
        else
        {
            logger.LogWarning("Handled {RequestName} in {ElapsedMs:0.0} ms with failure {ErrorCode}", name, elapsedMs, response.Error.Code);
        }

        return response;
    }
}
