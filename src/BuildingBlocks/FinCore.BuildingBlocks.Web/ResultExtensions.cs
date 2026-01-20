using FinCore.SharedKernel.Results;
using Microsoft.AspNetCore.Http;

namespace FinCore.BuildingBlocks.Web;

/// <summary>Maps <see cref="Result"/> failures to RFC 9457 ProblemDetails with a stable <c>code</c> (spec §28).</summary>
public static class ResultExtensions
{
    public static IResult ToProblem(this Result result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.IsSuccess)
        {
            throw new InvalidOperationException("A successful result cannot be converted to a problem.");
        }

        return ToProblem(result.Error);
    }

    public static IResult ToProblem(this Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var extensions = new Dictionary<string, object?> { ["code"] = error.Code };

        if (error is ValidationError validation)
        {
            return Results.ValidationProblem(
                validation.Errors.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal),
                detail: validation.Description,
                title: "Validation failed",
                extensions: extensions);
        }

        var (status, title) = error.Type switch
        {
            ErrorType.NotFound => (StatusCodes.Status404NotFound, "Not found"),
            ErrorType.Conflict => (StatusCodes.Status409Conflict, "Conflict"),
            ErrorType.Forbidden => (StatusCodes.Status403Forbidden, "Forbidden"),
            ErrorType.BusinessRule => (StatusCodes.Status422UnprocessableEntity, "Business rule violated"),
            ErrorType.Validation => (StatusCodes.Status400BadRequest, "Validation failed"),
            _ => (StatusCodes.Status400BadRequest, "Request failed"),
        };

        return Results.Problem(statusCode: status, title: title, detail: error.Description, extensions: extensions);
    }
}
