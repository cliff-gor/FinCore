namespace FinCore.SharedKernel.Results;

/// <summary>A typed business failure. <see cref="Code"/> is stable and machine-readable, e.g. <c>Organization.InstitutionNotFound</c>.</summary>
public record Error(string Code, string Description, ErrorType Type)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);

    public static Error Failure(string code, string description) => new(code, description, ErrorType.Failure);

    public static Error NotFound(string code, string description) => new(code, description, ErrorType.NotFound);

    public static Error Conflict(string code, string description) => new(code, description, ErrorType.Conflict);

    public static Error Forbidden(string code, string description) => new(code, description, ErrorType.Forbidden);

    public static Error BusinessRule(string code, string description) => new(code, description, ErrorType.BusinessRule);
}

/// <summary>Validation failure carrying per-field messages (maps to ProblemDetails <c>errors</c>).</summary>
public sealed record ValidationError(IReadOnlyDictionary<string, string[]> Errors)
    : Error("Validation.Failed", "One or more validation errors occurred.", ErrorType.Validation)
{
    public static ValidationError For(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}
