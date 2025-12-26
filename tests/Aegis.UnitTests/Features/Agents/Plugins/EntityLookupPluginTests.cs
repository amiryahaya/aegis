using Aegis.Api.Features.Agents.Plugins;
using Aegis.Domain.Common;
using Aegis.Domain.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Aegis.UnitTests.Features.Agents.Plugins;

public class EntityLookupPluginTests
{
    private readonly IGraphService _graphService;
    private readonly ILogger<EntityLookupPlugin> _logger;
    private readonly EntityLookupPlugin _plugin;

    public EntityLookupPluginTests()
    {
        _graphService = Substitute.For<IGraphService>();
        _logger = Substitute.For<ILogger<EntityLookupPlugin>>();
        _plugin = new EntityLookupPlugin(_graphService, _logger);
    }

    [Fact]
    public async Task SearchEntitiesAsync_WithValidType_ShouldReturnEntities()
    {
        // Arrange
        var entities = new List<GraphEntity>
        {
            new GraphEntity
            {
                Id = "person-123",
                Name = "John Doe",
                Type = EntityType.Person,
                Confidence = 0.95
            }
        };

        _graphService.SearchEntitiesAsync(EntityType.Person, null, 20, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<GraphEntity>>.Success(entities));

        // Act
        var result = await _plugin.SearchEntitiesAsync("Person", 20);

        // Assert
        result.Should().Contain("John Doe");
        result.Should().Contain("Person");
    }

    [Fact]
    public async Task SearchEntitiesAsync_WithEmptyType_ShouldReturnError()
    {
        // Act
        var result = await _plugin.SearchEntitiesAsync("", 20);

        // Assert
        result.Should().Contain("Entity type cannot be empty");
    }

    [Fact]
    public async Task SearchEntitiesAsync_WithInvalidType_ShouldReturnError()
    {
        // Act
        var result = await _plugin.SearchEntitiesAsync("InvalidType", 20);

        // Assert
        result.Should().Contain("Invalid entity type");
    }
}
