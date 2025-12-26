using Aegis.Api.Features.Agents.Plugins;
using Aegis.Domain.Common;
using Aegis.Domain.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Aegis.UnitTests.Features.Agents.Plugins;

public class TimelineBuilderPluginTests
{
    private readonly IGraphQueryService _graphQueryService;
    private readonly IGraphQueryBuilder _queryBuilder;
    private readonly ILogger<TimelineBuilderPlugin> _logger;
    private readonly TimelineBuilderPlugin _plugin;

    public TimelineBuilderPluginTests()
    {
        _graphQueryService = Substitute.For<IGraphQueryService>();
        _queryBuilder = Substitute.For<IGraphQueryBuilder>();
        _logger = Substitute.For<ILogger<TimelineBuilderPlugin>>();
        _plugin = new TimelineBuilderPlugin(_graphQueryService, _logger);

        // Setup fluent query builder chain
        _graphQueryService.Query().Returns(_queryBuilder);
        _queryBuilder.CreatedAfter(Arg.Any<DateTime>()).Returns(_queryBuilder);
        _queryBuilder.OrderBy(Arg.Any<string>(), Arg.Any<bool>()).Returns(_queryBuilder);
        _queryBuilder.Limit(Arg.Any<int>()).Returns(_queryBuilder);
    }

    [Fact]
    public async Task BuildTimelineAsync_WithValidEntityId_ShouldReturnTimeline()
    {
        // Arrange
        var entities = new List<GraphEntity>
        {
            new GraphEntity
            {
                Id = "event-123",
                Name = "Event 1",
                Type = EntityType.Campaign,
                FirstSeen = DateTime.UtcNow.AddDays(-7),
                Confidence = 0.9
            }
        };

        _queryBuilder.ExecuteAsync(Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<GraphEntity>>.Success(entities));

        // Act
        var result = await _plugin.BuildTimelineAsync("entity-123");

        // Assert
        result.Should().Contain("entity-123");
        result.Should().Contain("Event 1");
        result.Should().Contain("success");
    }

    [Fact]
    public async Task BuildTimelineAsync_WithEmptyEntityId_ShouldReturnError()
    {
        // Act
        var result = await _plugin.BuildTimelineAsync("");

        // Assert
        result.Should().Contain("Entity ID cannot be empty");
    }

    [Fact]
    public async Task BuildTimelineAsync_WithInvalidDate_ShouldReturnError()
    {
        // Act
        var result = await _plugin.BuildTimelineAsync("entity-123", startDate: "invalid-date");

        // Assert
        result.Should().Contain("Invalid start date format");
    }
}
