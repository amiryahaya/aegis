using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.Admin;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Aegis.UnitTests.Services.Admin;

public class AdminDashboardServiceTests
{
    private readonly ILogger<InMemoryAdminDashboardService> _logger;
    private readonly InMemoryAdminDashboardService _sut;

    public AdminDashboardServiceTests()
    {
        _logger = Substitute.For<ILogger<InMemoryAdminDashboardService>>();
        _sut = new InMemoryAdminDashboardService(_logger);
    }

    #region GetSystemHealthAsync Tests

    [Fact]
    public async Task GetSystemHealthAsync_ReturnsHealthStatus()
    {
        // Act
        var result = await _sut.GetSystemHealthAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.OverallHealth.Should().BeOneOf(HealthState.Healthy, HealthState.Degraded, HealthState.Unhealthy);
        result.Value.CheckedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetSystemHealthAsync_IncludesComponents()
    {
        // Act
        var result = await _sut.GetSystemHealthAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Components.Should().NotBeEmpty();
        result.Value.Components.Should().ContainKey("Database");
        result.Value.Components.Should().ContainKey("VectorStore");
    }

    [Fact]
    public async Task GetSystemHealthAsync_IncludesResourceMetrics()
    {
        // Act
        var result = await _sut.GetSystemHealthAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CpuUsagePercent.Should().BeGreaterOrEqualTo(0);
        result.Value.MemoryUsagePercent.Should().BeGreaterOrEqualTo(0);
        result.Value.DiskUsagePercent.Should().BeGreaterOrEqualTo(0);
    }

    #endregion

    #region GetOverviewAsync Tests

    [Fact]
    public async Task GetOverviewAsync_ReturnsOverview()
    {
        // Act
        var result = await _sut.GetOverviewAsync(new DashboardOverviewQuery());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.GeneratedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetOverviewAsync_IncludesUserMetrics()
    {
        // Act
        var result = await _sut.GetOverviewAsync(new DashboardOverviewQuery());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalUsers.Should().BeGreaterOrEqualTo(0);
        result.Value.ActiveUsersToday.Should().BeGreaterOrEqualTo(0);
        result.Value.ActiveUsersThisWeek.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task GetOverviewAsync_IncludesQueryMetrics()
    {
        // Act
        var result = await _sut.GetOverviewAsync(new DashboardOverviewQuery());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.QueriesThisHour.Should().BeGreaterOrEqualTo(0);
        result.Value.QueriesToday.Should().BeGreaterOrEqualTo(0);
        result.Value.AverageResponseTimeMs.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task GetOverviewAsync_IncludesCostMetrics()
    {
        // Act
        var result = await _sut.GetOverviewAsync(new DashboardOverviewQuery());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CostToday.Should().BeGreaterOrEqualTo(0);
        result.Value.CostThisMonth.Should().BeGreaterOrEqualTo(0);
        result.Value.TokensUsedToday.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task GetOverviewAsync_WithTrendsEnabled_IncludesTrends()
    {
        // Act
        var result = await _sut.GetOverviewAsync(new DashboardOverviewQuery { IncludeTrends = true });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserGrowthTrend.Should().NotBeNull();
        result.Value.QueryVolumeTrend.Should().NotBeNull();
        result.Value.CostTrend.Should().NotBeNull();
    }

    #endregion

    #region GetUserStatsAsync Tests

    [Fact]
    public async Task GetUserStatsAsync_ReturnsUserStats()
    {
        // Act
        var result = await _sut.GetUserStatsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalUsers.Should().BeGreaterOrEqualTo(0);
        result.Value.LastUpdated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetUserStatsAsync_IncludesUserBreakdown()
    {
        // Act
        var result = await _sut.GetUserStatsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ActiveUsers.Should().BeGreaterOrEqualTo(0);
        result.Value.InactiveUsers.Should().BeGreaterOrEqualTo(0);
        result.Value.DisabledUsers.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task GetUserStatsAsync_IncludesRoleBreakdown()
    {
        // Act
        var result = await _sut.GetUserStatsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UsersByRole.Should().NotBeNull();
    }

    #endregion

    #region GetWorkspaceStatsAsync Tests

    [Fact]
    public async Task GetWorkspaceStatsAsync_ReturnsWorkspaceStats()
    {
        // Act
        var result = await _sut.GetWorkspaceStatsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalWorkspaces.Should().BeGreaterOrEqualTo(0);
        result.Value.LastUpdated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetWorkspaceStatsAsync_IncludesStorageInfo()
    {
        // Act
        var result = await _sut.GetWorkspaceStatsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalStorageBytes.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task GetWorkspaceStatsAsync_IncludesTopWorkspaces()
    {
        // Act
        var result = await _sut.GetWorkspaceStatsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TopWorkspaces.Should().NotBeNull();
    }

    #endregion

    #region GetDataSourceHealthAsync Tests

    [Fact]
    public async Task GetDataSourceHealthAsync_ReturnsDataSourceHealth()
    {
        // Act
        var result = await _sut.GetDataSourceHealthAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetDataSourceHealthAsync_IncludesStatusInfo()
    {
        // Act
        var result = await _sut.GetDataSourceHealthAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        foreach (var ds in result.Value)
        {
            ds.Status.Should().BeOneOf(
                DataSourceStatus.Connected,
                DataSourceStatus.Syncing,
                DataSourceStatus.Error,
                DataSourceStatus.Disconnected,
                DataSourceStatus.Pending);
        }
    }

    #endregion

    #region GetIngestionStatusAsync Tests

    [Fact]
    public async Task GetIngestionStatusAsync_ReturnsIngestionStatus()
    {
        // Act
        var result = await _sut.GetIngestionStatusAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.State.Should().BeOneOf(PipelineState.Running, PipelineState.Idle, PipelineState.Paused, PipelineState.Error);
        result.Value.LastUpdated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetIngestionStatusAsync_IncludesQueueMetrics()
    {
        // Act
        var result = await _sut.GetIngestionStatusAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.QueuedDocuments.Should().BeGreaterOrEqualTo(0);
        result.Value.ProcessingDocuments.Should().BeGreaterOrEqualTo(0);
        result.Value.CompletedToday.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task GetIngestionStatusAsync_IncludesThroughput()
    {
        // Act
        var result = await _sut.GetIngestionStatusAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AverageProcessingTimeSeconds.Should().BeGreaterOrEqualTo(0);
        result.Value.ThroughputDocsPerMinute.Should().BeGreaterOrEqualTo(0);
    }

    #endregion

    #region GetAlertsAsync Tests

    [Fact]
    public async Task GetAlertsAsync_ReturnsAlerts()
    {
        // Arrange
        await _sut.CreateAlertAsync(new SystemAlert
        {
            Id = Guid.NewGuid(),
            Type = AlertType.SystemHealth,
            Severity = AlertSeverity.Warning,
            Title = "Test Alert",
            Message = "Test message",
            CreatedAt = DateTime.UtcNow
        });

        // Act
        var result = await _sut.GetAlertsAsync(new AlertQuery());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAlertsAsync_FiltersBySeverity()
    {
        // Arrange
        await _sut.CreateAlertAsync(new SystemAlert
        {
            Id = Guid.NewGuid(),
            Type = AlertType.SystemHealth,
            Severity = AlertSeverity.Info,
            Title = "Info Alert",
            Message = "Info message",
            CreatedAt = DateTime.UtcNow
        });
        await _sut.CreateAlertAsync(new SystemAlert
        {
            Id = Guid.NewGuid(),
            Type = AlertType.SecurityEvent,
            Severity = AlertSeverity.Critical,
            Title = "Critical Alert",
            Message = "Critical message",
            CreatedAt = DateTime.UtcNow
        });

        // Act
        var result = await _sut.GetAlertsAsync(new AlertQuery { MinSeverity = AlertSeverity.Error });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().OnlyContain(a => a.Severity >= AlertSeverity.Error);
    }

    [Fact]
    public async Task GetAlertsAsync_ExcludesAcknowledgedByDefault()
    {
        // Arrange
        var alertId = Guid.NewGuid();
        await _sut.CreateAlertAsync(new SystemAlert
        {
            Id = alertId,
            Type = AlertType.SystemHealth,
            Severity = AlertSeverity.Warning,
            Title = "Acknowledged Alert",
            Message = "Message",
            CreatedAt = DateTime.UtcNow,
            IsAcknowledged = true
        });

        // Act
        var result = await _sut.GetAlertsAsync(new AlertQuery { IncludeAcknowledged = false });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotContain(a => a.Id == alertId);
    }

    [Fact]
    public async Task GetAlertsAsync_RespectsLimit()
    {
        // Arrange
        for (int i = 0; i < 10; i++)
        {
            await _sut.CreateAlertAsync(new SystemAlert
            {
                Id = Guid.NewGuid(),
                Type = AlertType.SystemHealth,
                Severity = AlertSeverity.Info,
                Title = $"Alert {i}",
                Message = $"Message {i}",
                CreatedAt = DateTime.UtcNow
            });
        }

        // Act
        var result = await _sut.GetAlertsAsync(new AlertQuery { Limit = 5 });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCountLessOrEqualTo(5);
    }

    #endregion

    #region AcknowledgeAlertAsync Tests

    [Fact]
    public async Task AcknowledgeAlertAsync_WithValidId_AcknowledgesAlert()
    {
        // Arrange
        var alertId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await _sut.CreateAlertAsync(new SystemAlert
        {
            Id = alertId,
            Type = AlertType.SystemHealth,
            Severity = AlertSeverity.Warning,
            Title = "Alert",
            Message = "Message",
            CreatedAt = DateTime.UtcNow
        });

        // Act
        var result = await _sut.AcknowledgeAlertAsync(alertId, userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();

        var alerts = await _sut.GetAlertsAsync(new AlertQuery { IncludeAcknowledged = true });
        var alert = alerts.Value.FirstOrDefault(a => a.Id == alertId);
        alert.Should().NotBeNull();
        alert!.IsAcknowledged.Should().BeTrue();
        alert.AcknowledgedBy.Should().Be(userId);
    }

    [Fact]
    public async Task AcknowledgeAlertAsync_WithInvalidId_ReturnsNotFound()
    {
        // Act
        var result = await _sut.AcknowledgeAlertAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region GetActivityFeedAsync Tests

    [Fact]
    public async Task GetActivityFeedAsync_ReturnsActivityItems()
    {
        // Arrange
        await _sut.AddActivityAsync(new ActivityFeedItem
        {
            Id = Guid.NewGuid(),
            Type = ActivityType.UserLogin,
            Description = "User logged in",
            Timestamp = DateTime.UtcNow
        });

        // Act
        var result = await _sut.GetActivityFeedAsync(new ActivityFeedQuery());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetActivityFeedAsync_FiltersByType()
    {
        // Arrange
        await _sut.AddActivityAsync(new ActivityFeedItem
        {
            Id = Guid.NewGuid(),
            Type = ActivityType.UserLogin,
            Description = "User logged in",
            Timestamp = DateTime.UtcNow
        });
        await _sut.AddActivityAsync(new ActivityFeedItem
        {
            Id = Guid.NewGuid(),
            Type = ActivityType.DocumentUploaded,
            Description = "Document uploaded",
            Timestamp = DateTime.UtcNow
        });

        // Act
        var result = await _sut.GetActivityFeedAsync(new ActivityFeedQuery
        {
            Types = new List<ActivityType> { ActivityType.UserLogin }
        });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().OnlyContain(a => a.Type == ActivityType.UserLogin);
    }

    [Fact]
    public async Task GetActivityFeedAsync_OrdersByTimestampDescending()
    {
        // Arrange
        await _sut.AddActivityAsync(new ActivityFeedItem
        {
            Id = Guid.NewGuid(),
            Type = ActivityType.UserLogin,
            Description = "First",
            Timestamp = DateTime.UtcNow.AddMinutes(-5)
        });
        await _sut.AddActivityAsync(new ActivityFeedItem
        {
            Id = Guid.NewGuid(),
            Type = ActivityType.UserLogin,
            Description = "Second",
            Timestamp = DateTime.UtcNow
        });

        // Act
        var result = await _sut.GetActivityFeedAsync(new ActivityFeedQuery());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.First().Description.Should().Be("Second");
    }

    #endregion
}
