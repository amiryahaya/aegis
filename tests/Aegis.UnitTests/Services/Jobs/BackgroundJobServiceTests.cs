using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.Jobs;
using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Aegis.UnitTests.Services.Jobs;

public class BackgroundJobServiceTests
{
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly JobStorage _jobStorage;
    private readonly ILogger<HangfireBackgroundJobService> _logger;
    private readonly HangfireBackgroundJobService _sut;

    public BackgroundJobServiceTests()
    {
        _backgroundJobClient = Substitute.For<IBackgroundJobClient>();
        _jobStorage = Substitute.For<JobStorage>();
        _logger = Substitute.For<ILogger<HangfireBackgroundJobService>>();
        _sut = new HangfireBackgroundJobService(_backgroundJobClient, _jobStorage, _logger);
    }

    [Fact]
    public void GetJobStatus_WithUnknownJob_ShouldReturnUnknown()
    {
        // Arrange
        var connection = Substitute.For<IStorageConnection>();
        connection.GetJobData("unknown-job").Returns((JobData?)null);
        _jobStorage.GetConnection().Returns(connection);

        // Act
        var result = _sut.GetJobStatus("unknown-job");

        // Assert
        result.Should().Be(JobStatus.Unknown);
    }

    [Fact]
    public void Cancel_ShouldNotThrow()
    {
        // Act
        var act = () => _sut.Cancel("job-123");

        // Assert - Cancel should not throw even if job doesn't exist
        act.Should().NotThrow();
    }

    [Fact]
    public void Retry_ShouldNotThrow()
    {
        // Act
        var act = () => _sut.Retry("job-123");

        // Assert - Retry should not throw even if job doesn't exist
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithNullBackgroundJobClient_ShouldThrow()
    {
        // Act
        var act = () => new HangfireBackgroundJobService(null!, _jobStorage, _logger);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("backgroundJobClient");
    }

    [Fact]
    public void Constructor_WithNullJobStorage_ShouldThrow()
    {
        // Act
        var act = () => new HangfireBackgroundJobService(_backgroundJobClient, null!, _logger);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("jobStorage");
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrow()
    {
        // Act
        var act = () => new HangfireBackgroundJobService(_backgroundJobClient, _jobStorage, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }
}
