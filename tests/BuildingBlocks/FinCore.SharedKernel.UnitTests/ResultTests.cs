using FinCore.SharedKernel.Results;

namespace SharedKernelTests;

public sealed class ResultTests
{
    private static readonly Error SomeError = Error.NotFound("Test.NotFound", "missing");

    [Fact]
    public void Success_and_failure()
    {
        Result.Success().IsSuccess.ShouldBeTrue();
        var failed = Result.Failure(SomeError);
        failed.IsFailure.ShouldBeTrue();
        failed.Error.ShouldBe(SomeError);
    }

    [Fact]
    public void Generic_result_exposes_value_only_on_success()
    {
        Result<int> ok = 42;
        ok.Value.ShouldBe(42);

        Result<int> failed = SomeError;
        failed.IsFailure.ShouldBeTrue();
        Should.Throw<InvalidOperationException>(() => _ = failed.Value);
    }

    [Fact]
    public void Implicit_conversions_and_factories()
    {
        Result fromError = SomeError;
        fromError.Error.ShouldBe(SomeError);
        Result.CreateFailure(SomeError).IsFailure.ShouldBeTrue();
        Result<string>.CreateFailure(SomeError).Error.ShouldBe(SomeError);
        Result.Success("x").Value.ShouldBe("x");
        Result.Failure<string>(SomeError).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Invalid_combinations_are_rejected()
    {
        Should.Throw<ArgumentException>(() => Result.Failure(Error.None));
    }

    [Theory]
    [InlineData(ErrorType.Failure)]
    [InlineData(ErrorType.NotFound)]
    [InlineData(ErrorType.Conflict)]
    [InlineData(ErrorType.Forbidden)]
    [InlineData(ErrorType.BusinessRule)]
    public void Error_factories_set_type(ErrorType type)
    {
        var error = type switch
        {
            ErrorType.Failure => Error.Failure("c", "d"),
            ErrorType.NotFound => Error.NotFound("c", "d"),
            ErrorType.Conflict => Error.Conflict("c", "d"),
            ErrorType.Forbidden => Error.Forbidden("c", "d"),
            _ => Error.BusinessRule("c", "d"),
        };
        error.Type.ShouldBe(type);
        error.Code.ShouldBe("c");
    }

    [Fact]
    public void Validation_error_carries_field_messages()
    {
        var error = ValidationError.For("name", "too short");
        error.Type.ShouldBe(ErrorType.Validation);
        error.Code.ShouldBe("Validation.Failed");
        error.Errors["name"].ShouldBe(["too short"]);
    }
}
