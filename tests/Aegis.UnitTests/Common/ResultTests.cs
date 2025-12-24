using Aegis.Domain.Common;

namespace Aegis.UnitTests.Common;

public class ResultTests
{
    [Fact]
    public void Success_ShouldCreateSuccessResult()
    {
        // Arrange & Act
        var result = Result<int>.Success(42);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().Be(42);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Failure_ShouldCreateFailureResult()
    {
        // Arrange
        var error = new Error("Test.Error", "Something went wrong");

        // Act
        var result = Result<int>.Failure(error);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void Value_ShouldThrowOnFailureResult()
    {
        // Arrange
        var error = new Error("Test.Error", "Something went wrong");
        var result = Result<int>.Failure(error);

        // Act
        var act = () => result.Value;

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot access value of a failed result");
    }

    [Fact]
    public void Match_ShouldExecuteOnSuccessForSuccessResult()
    {
        // Arrange
        var result = Result<int>.Success(42);

        // Act
        var output = result.Match(
            onSuccess: value => $"Value: {value}",
            onFailure: error => $"Error: {error.Code}");

        // Assert
        output.Should().Be("Value: 42");
    }

    [Fact]
    public void Match_ShouldExecuteOnFailureForFailureResult()
    {
        // Arrange
        var error = new Error("Test.Error", "Something went wrong");
        var result = Result<int>.Failure(error);

        // Act
        var output = result.Match(
            onSuccess: value => $"Value: {value}",
            onFailure: err => $"Error: {err.Code}");

        // Assert
        output.Should().Be("Error: Test.Error");
    }

    [Fact]
    public void Map_ShouldTransformValueOnSuccess()
    {
        // Arrange
        var result = Result<int>.Success(42);

        // Act
        var mapped = result.Map(x => x.ToString());

        // Assert
        mapped.IsSuccess.Should().BeTrue();
        mapped.Value.Should().Be("42");
    }

    [Fact]
    public void Map_ShouldPropagateErrorOnFailure()
    {
        // Arrange
        var error = new Error("Test.Error", "Something went wrong");
        var result = Result<int>.Failure(error);

        // Act
        var mapped = result.Map(x => x.ToString());

        // Assert
        mapped.IsFailure.Should().BeTrue();
        mapped.Error.Should().Be(error);
    }

    [Fact]
    public void Bind_ShouldChainSuccessfulResults()
    {
        // Arrange
        var result = Result<int>.Success(42);

        // Act
        var bound = result.Bind(x => Result<string>.Success(x.ToString()));

        // Assert
        bound.IsSuccess.Should().BeTrue();
        bound.Value.Should().Be("42");
    }

    [Fact]
    public void Bind_ShouldShortCircuitOnFailure()
    {
        // Arrange
        var error = new Error("Test.Error", "Something went wrong");
        var result = Result<int>.Failure(error);

        // Act
        var bound = result.Bind(x => Result<string>.Success(x.ToString()));

        // Assert
        bound.IsFailure.Should().BeTrue();
        bound.Error.Should().Be(error);
    }

    [Fact]
    public void ImplicitConversion_ShouldCreateSuccessFromValue()
    {
        // Arrange & Act
        Result<int> result = 42;

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void ImplicitConversion_ShouldCreateFailureFromError()
    {
        // Arrange
        var error = new Error("Test.Error", "Something went wrong");

        // Act
        Result<int> result = error;

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }
}
