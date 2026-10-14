namespace FinCore.BuildingBlocks.Application.Persistence;

/// <summary>
/// Thrown by a module's unit of work when another request changed the same aggregate first (xmin check, spec §25).
/// Handlers translate it into a 409 with a specific code, e.g. a second concurrent approval of the same request.
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException()
    {
    }

    public ConcurrencyConflictException(string message)
        : base(message)
    {
    }

    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
