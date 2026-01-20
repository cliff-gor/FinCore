using FinCore.SharedKernel.Results;

namespace FinCore.BuildingBlocks.Application.Messaging;

/// <summary>In-house request dispatcher (spec §3: avoids commercially licensed mediators).</summary>
public interface IDispatcher
{
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        where TResponse : Result;
}
