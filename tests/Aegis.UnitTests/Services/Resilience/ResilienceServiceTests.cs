using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.Resilience;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Aegis.UnitTests.Services.Resilience;

public class ResilienceServiceTests
{
    private readonly ILogger<PollyResilienceService> _logger;
    private readonly PollyResilienceService _sut;

    public ResilienceServiceTests()
    {
        _logger = Substitute.For<ILogger<PollyResilienceService>>();
        _sut = new PollyResilienceService(_logger);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOperationSucceeds_ShouldReturnSuccess()
    {
        // Arrange
        var expectedResult = "test-result";
        Task<string> Operation(CancellationToken ct) => Task.FromResult(expectedResult);

        // Act
        var result = await _sut.ExecuteAsync("test-operation", Operation);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedResult);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOperationFails_ShouldReturnFailure()
    {
        // Arrange
        Task<string> Operation(CancellationToken ct) =>
            throw new InvalidOperationException("Test error");

        // Act
        var result = await _sut.ExecuteAsync("test-operation", Operation);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("Test error");
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_ShouldReturnCancelledError()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        Task<string> Operation(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult("never");
        }

        // Act
        var result = await _sut.ExecuteAsync("test-operation", Operation, cts.Token);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Contain("Cancelled");
    }

    [Fact]
    public async Task ExecuteAsync_WithCustomOptions_ShouldApplyOptions()
    {
        // Arrange
        var options = new ResilienceOptions
        {
            MaxRetryAttempts = 1,
            Timeout = TimeSpan.FromSeconds(5)
        };
        var expectedResult = 42;
        Task<int> Operation(CancellationToken ct) => Task.FromResult(expectedResult);

        // Act
        var result = await _sut.ExecuteAsync("test-operation", Operation, options);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedResult);
    }

    [Fact]
    public void GetCircuitState_ForUnknownOperation_ShouldReturnClosed()
    {
        // Act
        var state = _sut.GetCircuitState("unknown-operation");

        // Assert
        state.Should().Be(CircuitBreakerState.Closed);
    }

    [Fact]
    public void ResetCircuit_ShouldNotThrow()
    {
        // Act
        var act = () => _sut.ResetCircuit("test-operation");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrow()
    {
        // Act
        var act = () => new PollyResilienceService(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public async Task ExecuteAsync_WithNullOperationKey_ShouldThrow()
    {
        // Arrange
        Task<string> Operation(CancellationToken ct) => Task.FromResult("test");

        // Act
        var act = async () => await _sut.ExecuteAsync<string>(null!, Operation);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("operationKey");
    }

    [Fact]
    public async Task ExecuteAsync_WithNullOperation_ShouldThrow()
    {
        // Act
        var act = async () => await _sut.ExecuteAsync<string>("test", null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("operation");
    }

    [Fact]
    public async Task ExecuteAsync_WithNullOptions_ShouldThrow()
    {
        // Arrange
        Task<string> Operation(CancellationToken ct) => Task.FromResult("test");

        // Act
        var act = async () => await _sut.ExecuteAsync("test", Operation, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("options");
    }

    [Fact]
    public async Task ExecuteWithResultAsync_WhenInnerResultSucceeds_ShouldReturnSuccess()
    {
        // Arrange
        var expectedValue = "success-value";
        Task<Domain.Common.Result<string>> Operation(CancellationToken ct) =>
            Task.FromResult(Domain.Common.Result<string>.Success(expectedValue));

        // Act
        var result = await _sut.ExecuteWithResultAsync("test-operation", Operation);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedValue);
    }

    [Fact]
    public void ResilienceOptions_ForLlmApi_ShouldHaveCorrectDefaults()
    {
        // Act
        var options = ResilienceOptions.ForLlmApi();

        // Assert
        options.MaxRetryAttempts.Should().Be(3);
        options.Timeout.Should().Be(TimeSpan.FromMinutes(2));
        options.CircuitBreakerBreakDuration.Should().Be(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void ResilienceOptions_ForVectorDb_ShouldHaveCorrectDefaults()
    {
        // Act
        var options = ResilienceOptions.ForVectorDb();

        // Assert
        options.MaxRetryAttempts.Should().Be(3);
        options.Timeout.Should().Be(TimeSpan.FromSeconds(30));
        options.RetryBaseDelay.Should().Be(TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void ResilienceOptions_ForDatabase_ShouldHaveCorrectDefaults()
    {
        // Act
        var options = ResilienceOptions.ForDatabase();

        // Assert
        options.MaxRetryAttempts.Should().Be(3);
        options.Timeout.Should().Be(TimeSpan.FromSeconds(15));
        options.RetryBaseDelay.Should().Be(TimeSpan.FromMilliseconds(200));
    }

    [Fact]
    public void ResilienceOptions_ForExternalApi_ShouldHaveCorrectDefaults()
    {
        // Act
        var options = ResilienceOptions.ForExternalApi();

        // Assert
        options.MaxRetryAttempts.Should().Be(2);
        options.Timeout.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void DefaultResilienceOptions_ShouldHaveCorrectDefaults()
    {
        // Act
        var options = new ResilienceOptions();

        // Assert
        options.MaxRetryAttempts.Should().Be(3);
        options.RetryBaseDelay.Should().Be(TimeSpan.FromSeconds(1));
        options.MaxRetryDelay.Should().Be(TimeSpan.FromSeconds(30));
        options.Timeout.Should().Be(TimeSpan.FromSeconds(30));
        options.CircuitBreakerFailureThreshold.Should().Be(5);
        options.CircuitBreakerBreakDuration.Should().Be(TimeSpan.FromSeconds(30));
        options.UseJitter.Should().BeTrue();
    }
}
