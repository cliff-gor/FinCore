namespace FinCore.SharedKernel.Integration;

/// <summary>
/// A fact published by one module for others. Immutable, self-contained, serialized to JSON in the outbox.
/// Integration event records live in a module's <c>.Contracts</c> project.
/// </summary>
public interface IIntegrationEvent
{
    Guid Id { get; }

    DateTime OccurredOnUtc { get; }
}
