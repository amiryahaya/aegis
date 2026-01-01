using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.Admin;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Aegis.UnitTests.Services.Admin;

public class UsageAnalyticsServiceTests
{
    private readonly ILogger<InMemoryUsageAnalyticsService> _logger;
    private readonly InMemoryUsageAnalyticsService _sut;

    public UsageAnalyticsServiceTests()
    {
        _logger = Substitute.For<ILogger<InMemoryUsageAnalyticsService>>();
        _sut = new InMemoryUsageAnalyticsService(_logger);
    }

    #region TrackEventAsync Tests

    [Fact]
    public async Task TrackEventAsync_WithValidRequest_ReturnsEvent()
    {
        // Arrange
        var request = new UsageEventRequest
        {
            EventType = UsageEventType.Query,
            UserId = Guid.NewGuid(),
            WorkspaceId = Guid.NewGuid(),
            TokensUsed = 100,
            Cost = 0.01m,
            Duration = TimeSpan.FromMilliseconds(500)
        };

        // Act
        var result = await _sut.TrackEventAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EventType.Should().Be(UsageEventType.Query);
        result.Value.TokensUsed.Should().Be(100);
        result.Value.Cost.Should().Be(0.01m);
        result.Value.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task TrackEventAsync_WithCacheHit_TracksCorrectly()
    {
        // Arrange
        var request = new UsageEventRequest
        {
            EventType = UsageEventType.CacheHit,
            UserId = Guid.NewGuid(),
            CacheHit = true,
            TokensUsed = 0,
            Cost = 0
        };

        // Act
        var result = await _sut.TrackEventAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CacheHit.Should().BeTrue();
    }

    [Fact]
    public async Task TrackEventAsync_WithMetadata_StoresMetadata()
    {
        // Arrange
        var request = new UsageEventRequest
        {
            EventType = UsageEventType.Query,
            Metadata = new Dictionary<string, string>
            {
                { "model", "gpt-4" },
                { "temperature", "0.7" }
            }
        };

        // Act
        var result = await _sut.TrackEventAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Metadata.Should().ContainKey("model");
        result.Value.Metadata!["model"].Should().Be("gpt-4");
    }

    #endregion

    #region GetSummaryAsync Tests

    [Fact]
    public async Task GetSummaryAsync_WithEvents_ReturnsSummary()
    {
        // Arrange
        for (int i = 0; i < 10; i++)
        {
            await _sut.TrackEventAsync(new UsageEventRequest
            {
                EventType = UsageEventType.Query,
                UserId = Guid.NewGuid()
            });
        }

        // Act
        var result = await _sut.GetSummaryAsync(new UsageSummaryQuery());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalEvents.Should().Be(10);
        result.Value.TotalQueries.Should().Be(10);
    }

    [Fact]
    public async Task GetSummaryAsync_CalculatesTokensCorrectly()
    {
        // Arrange
        await _sut.TrackEventAsync(new UsageEventRequest
        {
            EventType = UsageEventType.Query,
            InputTokens = 50,
            OutputTokens = 100,
            TokensUsed = 150
        });
        await _sut.TrackEventAsync(new UsageEventRequest
        {
            EventType = UsageEventType.Query,
            InputTokens = 30,
            OutputTokens = 70,
            TokensUsed = 100
        });

        // Act
        var result = await _sut.GetSummaryAsync(new UsageSummaryQuery());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalTokensUsed.Should().Be(250);
        result.Value.TotalInputTokens.Should().Be(80);
        result.Value.TotalOutputTokens.Should().Be(170);
    }

    [Fact]
    public async Task GetSummaryAsync_CalculatesCostCorrectly()
    {
        // Arrange
        await _sut.TrackEventAsync(new UsageEventRequest
        {
            EventType = UsageEventType.Query,
            Cost = 0.05m
        });
        await _sut.TrackEventAsync(new UsageEventRequest
        {
            EventType = UsageEventType.Query,
            Cost = 0.03m
        });

        // Act
        var result = await _sut.GetSummaryAsync(new UsageSummaryQuery());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCost.Should().Be(0.08m);
    }

    [Fact]
    public async Task GetSummaryAsync_CalculatesCacheHitRate()
    {
        // Arrange
        await _sut.TrackEventAsync(new UsageEventRequest { EventType = UsageEventType.Query, CacheHit = true });
        await _sut.TrackEventAsync(new UsageEventRequest { EventType = UsageEventType.Query, CacheHit = true });
        await _sut.TrackEventAsync(new UsageEventRequest { EventType = UsageEventType.Query, CacheHit = false });

        // Act
        var result = await _sut.GetSummaryAsync(new UsageSummaryQuery());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CacheHits.Should().Be(2);
        result.Value.CacheMisses.Should().Be(1);
        result.Value.CacheHitRate.Should().BeApproximately(66.67, 0.1);
    }

    [Fact]
    public async Task GetSummaryAsync_WithUserFilter_FiltersResults()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await _sut.TrackEventAsync(new UsageEventRequest { EventType = UsageEventType.Query, UserId = userId });
        await _sut.TrackEventAsync(new UsageEventRequest { EventType = UsageEventType.Query, UserId = Guid.NewGuid() });

        // Act
        var result = await _sut.GetSummaryAsync(new UsageSummaryQuery { UserId = userId });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalEvents.Should().Be(1);
    }

    [Fact]
    public async Task GetSummaryAsync_WithDateRange_FiltersResults()
    {
        // Arrange
        await _sut.TrackEventAsync(new UsageEventRequest { EventType = UsageEventType.Query });

        // Act
        var result = await _sut.GetSummaryAsync(new UsageSummaryQuery
        {
            FromDate = DateTime.UtcNow.AddMinutes(-1),
            ToDate = DateTime.UtcNow.AddMinutes(1)
        });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalEvents.Should().Be(1);
    }

    #endregion

    #region GetTrendsAsync Tests

    [Fact]
    public async Task GetTrendsAsync_ReturnsDailyTrends()
    {
        // Arrange
        await CreateTestEvents(20);

        // Act
        var result = await _sut.GetTrendsAsync(new UsageTrendsQuery
        {
            Granularity = TrendGranularity.Daily
        });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.DataPoints.Should().NotBeEmpty();
        result.Value.Granularity.Should().Be(TrendGranularity.Daily);
    }

    [Fact]
    public async Task GetTrendsAsync_ReturnsHourlyTrends()
    {
        // Arrange
        await CreateTestEvents(10);

        // Act
        var result = await _sut.GetTrendsAsync(new UsageTrendsQuery
        {
            Granularity = TrendGranularity.Hourly
        });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.DataPoints.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetTrendsAsync_IncludesMetricSummaries()
    {
        // Arrange
        await _sut.TrackEventAsync(new UsageEventRequest
        {
            EventType = UsageEventType.Query,
            TokensUsed = 100,
            Cost = 0.01m
        });
        await _sut.TrackEventAsync(new UsageEventRequest
        {
            EventType = UsageEventType.Query,
            TokensUsed = 200,
            Cost = 0.02m
        });

        // Act
        var result = await _sut.GetTrendsAsync(new UsageTrendsQuery
        {
            Metrics = new List<TrendMetric> { TrendMetric.Queries, TrendMetric.Tokens, TrendMetric.Cost }
        });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.MetricSummaries.Should().ContainKey(TrendMetric.Queries);
    }

    #endregion

    #region GetTopUsersAsync Tests

    [Fact]
    public async Task GetTopUsersAsync_ReturnsTopUsers()
    {
        // Arrange
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        await _sut.TrackEventAsync(new UsageEventRequest { EventType = UsageEventType.Query, UserId = user1 });
        await _sut.TrackEventAsync(new UsageEventRequest { EventType = UsageEventType.Query, UserId = user1 });
        await _sut.TrackEventAsync(new UsageEventRequest { EventType = UsageEventType.Query, UserId = user2 });

        // Act
        var result = await _sut.GetTopUsersAsync(new TopUsersQuery { Limit = 10 });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.First().UserId.Should().Be(user1);
        result.Value.First().QueryCount.Should().Be(2);
    }

    [Fact]
    public async Task GetTopUsersAsync_SortsByTokens()
    {
        // Arrange
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        await _sut.TrackEventAsync(new UsageEventRequest { EventType = UsageEventType.Query, UserId = user1, TokensUsed = 100 });
        await _sut.TrackEventAsync(new UsageEventRequest { EventType = UsageEventType.Query, UserId = user2, TokensUsed = 500 });

        // Act
        var result = await _sut.GetTopUsersAsync(new TopUsersQuery { SortBy = UserSortBy.Tokens });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.First().UserId.Should().Be(user2);
        result.Value.First().TokensUsed.Should().Be(500);
    }

    [Fact]
    public async Task GetTopUsersAsync_RespectsLimit()
    {
        // Arrange
        for (int i = 0; i < 15; i++)
        {
            await _sut.TrackEventAsync(new UsageEventRequest
            {
                EventType = UsageEventType.Query,
                UserId = Guid.NewGuid()
            });
        }

        // Act
        var result = await _sut.GetTopUsersAsync(new TopUsersQuery { Limit = 5 });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(5);
    }

    #endregion

    #region GetWorkspaceUsageAsync Tests

    [Fact]
    public async Task GetWorkspaceUsageAsync_ReturnsWorkspaceStats()
    {
        // Arrange
        var ws1 = Guid.NewGuid();
        var ws2 = Guid.NewGuid();
        await _sut.TrackEventAsync(new UsageEventRequest { EventType = UsageEventType.Query, WorkspaceId = ws1 });
        await _sut.TrackEventAsync(new UsageEventRequest { EventType = UsageEventType.Query, WorkspaceId = ws1 });
        await _sut.TrackEventAsync(new UsageEventRequest { EventType = UsageEventType.Query, WorkspaceId = ws2 });

        // Act
        var result = await _sut.GetWorkspaceUsageAsync(new WorkspaceUsageQuery());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.First().WorkspaceId.Should().Be(ws1);
        result.Value.First().QueryCount.Should().Be(2);
    }

    [Fact]
    public async Task GetWorkspaceUsageAsync_TracksUniqueUsers()
    {
        // Arrange
        var ws = Guid.NewGuid();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        await _sut.TrackEventAsync(new UsageEventRequest { EventType = UsageEventType.Query, WorkspaceId = ws, UserId = user1 });
        await _sut.TrackEventAsync(new UsageEventRequest { EventType = UsageEventType.Query, WorkspaceId = ws, UserId = user1 });
        await _sut.TrackEventAsync(new UsageEventRequest { EventType = UsageEventType.Query, WorkspaceId = ws, UserId = user2 });

        // Act
        var result = await _sut.GetWorkspaceUsageAsync(new WorkspaceUsageQuery());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.First().UniqueUsers.Should().Be(2);
    }

    #endregion

    #region GetCostAnalysisAsync Tests

    [Fact]
    public async Task GetCostAnalysisAsync_CalculatesTotalCost()
    {
        // Arrange
        await _sut.TrackEventAsync(new UsageEventRequest { EventType = UsageEventType.Query, Cost = 0.05m, Model = "gpt-4" });
        await _sut.TrackEventAsync(new UsageEventRequest { EventType = UsageEventType.Query, Cost = 0.02m, Model = "gpt-3.5" });
        await _sut.TrackEventAsync(new UsageEventRequest { EventType = UsageEventType.Query, Cost = 0.03m, Model = "gpt-4" });

        // Act
        var result = await _sut.GetCostAnalysisAsync(new CostAnalysisQuery());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCost.Should().Be(0.10m);
    }

    [Fact]
    public async Task GetCostAnalysisAsync_BreaksDownByModel()
    {
        // Arrange
        await _sut.TrackEventAsync(new UsageEventRequest { EventType = UsageEventType.Query, Cost = 0.05m, Model = "gpt-4" });
        await _sut.TrackEventAsync(new UsageEventRequest { EventType = UsageEventType.Query, Cost = 0.02m, Model = "gpt-3.5" });

        // Act
        var result = await _sut.GetCostAnalysisAsync(new CostAnalysisQuery());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CostByModel.Should().ContainKey("gpt-4");
        result.Value.CostByModel["gpt-4"].Should().Be(0.05m);
        result.Value.CostByModel["gpt-3.5"].Should().Be(0.02m);
    }

    [Fact]
    public async Task GetCostAnalysisAsync_CalculatesCacheSavings()
    {
        // Arrange
        await _sut.TrackEventAsync(new UsageEventRequest
        {
            EventType = UsageEventType.CacheHit,
            CacheHit = true,
            Cost = 0,
            Metadata = new Dictionary<string, string> { { "savedCost", "0.05" } }
        });

        // Act
        var result = await _sut.GetCostAnalysisAsync(new CostAnalysisQuery());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CostSavedByCache.Should().Be(0.05m);
    }

    [Fact]
    public async Task GetCostAnalysisAsync_ProjectsMonthlyCost()
    {
        // Arrange
        await _sut.TrackEventAsync(new UsageEventRequest { EventType = UsageEventType.Query, Cost = 1.00m });

        // Act
        var result = await _sut.GetCostAnalysisAsync(new CostAnalysisQuery { IncludeProjections = true });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ProjectedMonthlyCost.Should().BeGreaterThan(0);
    }

    #endregion

    #region GetRealTimeMetricsAsync Tests

    [Fact]
    public async Task GetRealTimeMetricsAsync_ReturnsCurrentMetrics()
    {
        // Arrange
        await _sut.TrackEventAsync(new UsageEventRequest
        {
            EventType = UsageEventType.Query,
            UserId = Guid.NewGuid(),
            Duration = TimeSpan.FromMilliseconds(200)
        });

        // Act
        var result = await _sut.GetRealTimeMetricsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.Value.QueriesLastHour.Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task GetRealTimeMetricsAsync_TracksActiveUsers()
    {
        // Arrange
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        await _sut.TrackEventAsync(new UsageEventRequest { EventType = UsageEventType.Query, UserId = user1 });
        await _sut.TrackEventAsync(new UsageEventRequest { EventType = UsageEventType.Query, UserId = user2 });

        // Act
        var result = await _sut.GetRealTimeMetricsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ActiveUsers.Should().Be(2);
    }

    #endregion

    #region Helper Methods

    private async Task CreateTestEvents(int count)
    {
        var random = new Random();
        var eventTypes = Enum.GetValues<UsageEventType>();

        for (int i = 0; i < count; i++)
        {
            await _sut.TrackEventAsync(new UsageEventRequest
            {
                EventType = eventTypes[random.Next(eventTypes.Length)],
                UserId = Guid.NewGuid(),
                WorkspaceId = Guid.NewGuid(),
                TokensUsed = random.Next(50, 500),
                Cost = (decimal)(random.NextDouble() * 0.1),
                Duration = TimeSpan.FromMilliseconds(random.Next(100, 2000)),
                CacheHit = random.NextDouble() > 0.5
            });
        }
    }

    #endregion
}
