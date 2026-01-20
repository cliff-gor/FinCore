namespace FinCore.BuildingBlocks.Application.Persistence;

/// <summary>
/// Thrown by a module's unit of work when the database rejects a write because of a unique index.
/// Handlers translate it into a Conflict result (e.g. duplicate codes created concurrently).
/// </summary>
public sealed class UniqueConstraintViolationException : Exception
{
    public UniqueConstraintViolationException()
    {
    }

    public UniqueConstraintViolationException(string message)
        : base(message)
    {
    }

    public UniqueConstraintViolationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public string? ConstraintName { get; private init; }

    public static UniqueConstraintViolationException For(string? constraintName, Exception innerException) =>
        new($"Unique constraint '{constraintName}' was violated.", innerException) { ConstraintName = constraintName };
}
