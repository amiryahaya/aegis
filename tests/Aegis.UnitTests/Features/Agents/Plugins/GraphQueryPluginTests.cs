using Aegis.Api.Features.Agents.Plugins;
using Aegis.Domain.Common;
using Aegis.Domain.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Aegis.UnitTests.Features.Agents.Plugins;

public class GraphQueryPluginTests
{
    private readonly IGraphService _graphService;
    private readonly ILogger<GraphQueryPlugin> _logger;
    private readonly GraphQueryPlugin _plugin;

    public GraphQueryPluginTests()
    {
        _graphService = Substitute.For<IGraphService>();
        _logger = Substitute.For<ILogger<GraphQueryPlugin>>();
        _plugin = new GraphQueryPlugin(_graphService, _logger);
    }

    [Fact]
    public async Task GetEntityNetworkAsync_WithValidEntityId_ShouldReturnNetwork()
    {
        // Arrange
        var entityId = "person-123";
        var expectedNetwork = new EntityNetwork
        {
            Entity = new GraphEntity
            {
                Id = entityId,
                Name = "John Doe",
                Type = EntityType.Person,
                Confidence = 0.95
            },
            Relationships = new List<EntityRelationship>
            {
                new EntityRelationship
                {
                    Type = "WORKS_FOR",
                    Entity = new GraphEntity
                    {
                        Id = "org-456",
                        Name = "Acme Corp",
                        Type = EntityType.Organization
                    },
                    Confidence = 0.9
                }
            }
        };

        _graphService.GetEntityNetworkAsync(entityId, 2, Arg.Any<CancellationToken>())
            .Returns(Result<EntityNetwork>.Success(expectedNetwork));

        // Act
        var result = await _plugin.GetEntityNetworkAsync(entityId, 2);

        // Assert
        result.Should().Contain("John Doe");
        result.Should().Contain("WORKS_FOR");
        result.Should().Contain("Acme Corp");
    }

    [Fact]
    public async Task FindPathAsync_WithValidEntities_ShouldReturnPaths()
    {
        // Arrange
        var sourceId = "person-123";
        var targetId = "person-456";
        var expectedPaths = new List<EntityPath>
        {
            new EntityPath
            {
                Entities = new List<GraphEntity>
                {
                    new GraphEntity { Id = sourceId, Name = "John", Type = EntityType.Person },
                    new GraphEntity { Id = "org-789", Name = "Acme", Type = EntityType.Organization },
                    new GraphEntity { Id = targetId, Name = "Jane", Type = EntityType.Person }
                },
                RelationshipTypes = new List<string> { "WORKS_FOR", "EMPLOYS" }
            }
        };

        _graphService.FindPathsAsync(sourceId, targetId, 5, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<EntityPath>>.Success(expectedPaths));

        // Act
        var result = await _plugin.FindPathAsync(sourceId, targetId, 5);

        // Assert
        result.Should().Contain("John");
        result.Should().Contain("Jane");
        result.Should().Contain("WORKS_FOR");
        result.Should().Contain("EMPLOYS");
    }

    [Fact]
    public async Task GetEntityNetworkAsync_WithEmptyEntityId_ShouldReturnError()
    {
        // Act
        var result = await _plugin.GetEntityNetworkAsync("", 2);

        // Assert
        result.Should().Contain("Entity ID cannot be empty");
    }

    [Fact]
    public async Task FindPathAsync_WithEmptySourceId_ShouldReturnError()
    {
        // Act
        var result = await _plugin.FindPathAsync("", "target-123", 5);

        // Assert
        result.Should().Contain("Source entity ID cannot be empty");
    }
}
