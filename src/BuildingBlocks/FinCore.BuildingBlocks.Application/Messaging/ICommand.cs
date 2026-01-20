using FinCore.SharedKernel.Results;

namespace FinCore.BuildingBlocks.Application.Messaging;

/// <summary>Marker for every request the dispatcher can handle.</summary>
public interface IRequest<TResponse>
    where TResponse : Result;

/// <summary>A state-changing request with no payload on success.</summary>
public interface ICommand : IRequest<Result>;

/// <summary>A state-changing request returning a value on success.</summary>
public interface ICommand<TValue> : IRequest<Result<TValue>>;

/// <summary>A read-only request.</summary>
public interface IQuery<TValue> : IRequest<Result<TValue>>;
