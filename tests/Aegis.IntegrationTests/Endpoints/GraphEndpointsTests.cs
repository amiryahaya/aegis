using System.Net;
using System.Net.Http.Json;
using Aegis.Domain.Services;
using Aegis.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Aegis.IntegrationTests.Endpoints;

[Collection("Integration")]
public class GraphEndpointsTests : IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly IGraphService? _graphService;
    private readonly bool _neo4jAvailable;
    private string? _testEntityId1;
    private string? _testEntityId2;

    public GraphEndpointsTests(AegisApiFactory factory)
    {
        _client = factory.CreateClient();

        try
        {
            _graphService = factory.Services.GetService<IGraphService>();
            _neo4jAvailable = _graphService != null;
        }
        catch
        {
            _neo4jAvailable = false;
        }
    }

    public async Task InitializeAsync()
    {
        if (!_neo4jAvailable || _graphService == null) return;

        // Create test entities
        var entity1 = new GraphEntity
        {
            Id = "test-graph-api-1",
            Type = EntityType.ThreatActor,
            Name = "APT28",
            Properties = new Dictionary<string, object>
            {
                ["description"] = "Russian APT group"
            },
            Confidence = 0.95
        };

        var entity2 = new GraphEntity
        {
            Id = "test-graph-api-2",
            Type = EntityType.Malware,
            Name = "WannaCry",
            Properties = new Dictionary<string, object>
            {
                ["type"] = "Ransomware"
            },
            Confidence = 0.98
        };

        await _graphService.UpsertEntityAsync(entity1);
        await _graphService.UpsertEntityAsync(entity2);
        await _graphService.CreateRelationshipAsync("test-graph-api-1", "test-graph-api-2", GraphRelationshipTypes.USES);

        _testEntityId1 = entity1.Id;
        _testEntityId2 = entity2.Id;
    }

    public async Task DisposeAsync()
    {
        if (!_neo4jAvailable || _graphService == null) return;

        // Clean up test entities
        if (_testEntityId1 != null)
        {
            await _graphService.DeleteEntityAsync(_testEntityId1);
        }

        if (_testEntityId2 != null)
        {
            await _graphService.DeleteEntityAsync(_testEntityId2);
        }
    }

    [Fact]
    public async Task GetEntity_WithValidId_ShouldReturnEntity()
    {
        if (!_neo4jAvailable) return;

        // Act
        var response = await _client.GetAsync($"/api/graph/entities/{_testEntityId1}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var entity = await response.Content.ReadFromJsonAsync<EntityResponse>();
        entity.Should().NotBeNull();
        entity!.Id.Should().Be(_testEntityId1);
        entity.Name.Should().Be("APT28");
        entity.Type.Should().Be("ThreatActor");
        entity.Confidence.Should().BeGreaterThan(0.9);
    }

    [Fact]
    public async Task GetEntity_WithInvalidId_ShouldReturnNotFound()
    {
        if (!_neo4jAvailable) return;

        // Act
        var response = await _client.GetAsync("/api/graph/entities/non-existent-id");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SearchEntities_WithTypeFilter_ShouldReturnMatchingEntities()
    {
        if (!_neo4jAvailable) return;

        // Act
        var response = await _client.GetAsync("/api/graph/entities?type=ThreatActor&limit=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<SearchEntitiesResponse>();
        result.Should().NotBeNull();
        result!.Entities.Should().NotBeEmpty();
        result.Entities.Should().OnlyContain(e => e.Type == "ThreatActor");
    }

    [Fact]
    public async Task SearchEntities_WithConfidenceFilter_ShouldReturnHighConfidenceEntities()
    {
        if (!_neo4jAvailable) return;

        // Act
        var response = await _client.GetAsync("/api/graph/entities?minConfidence=0.9&limit=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<SearchEntitiesResponse>();
        result.Should().NotBeNull();
        result!.Entities.Should().NotBeEmpty();
        result.Entities.Should().OnlyContain(e => e.Confidence >= 0.9);
    }

    [Fact]
    public async Task GetEntityNetwork_WithValidId_ShouldReturnNetwork()
    {
        if (!_neo4jAvailable) return;

        // Act
        var response = await _client.GetAsync($"/api/graph/entities/{_testEntityId1}/network?maxHops=1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var network = await response.Content.ReadFromJsonAsync<EntityNetworkResponse>();
        network.Should().NotBeNull();
        network!.CentralEntity.Id.Should().Be(_testEntityId1);
        network.Relationships.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetSimilarEntities_WithValidId_ShouldReturnSimilarEntities()
    {
        if (!_neo4jAvailable) return;

        // Act
        var response = await _client.GetAsync($"/api/graph/entities/{_testEntityId1}/similar?limit=5");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var similarEntities = await response.Content.ReadFromJsonAsync<List<SimilarEntityResponse>>();
        similarEntities.Should().NotBeNull();
        // Similar entities may or may not exist depending on graph state
    }

    [Fact]
    public async Task FindPaths_BetweenConnectedEntities_ShouldReturnPaths()
    {
        if (!_neo4jAvailable) return;

        // Act
        var response = await _client.GetAsync($"/api/graph/entities/{_testEntityId1}/paths/{_testEntityId2}?maxDepth=3");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var paths = await response.Content.ReadFromJsonAsync<PathsResponse>();
        paths.Should().NotBeNull();
        paths!.Paths.Should().NotBeEmpty();
        paths.Paths.First().Entities.Should().HaveCountGreaterThan(1);
        paths.Paths.First().Length.Should().BeGreaterThan(0);
    }
}

// Response DTOs (matching GraphModule)
public record EntityResponse(
    string Id,
    string Type,
    string Name,
    Dictionary<string, object> Properties,
    double Confidence,
    DateTime? FirstSeen,
    DateTime? LastSeen);

public record SearchEntitiesResponse(
    List<EntityResponse> Entities,
    int TotalCount);

public record RelationshipResponse(
    string Type,
    EntityResponse Entity,
    Dictionary<string, object> Properties,
    double Confidence);

public record EntityNetworkResponse(
    EntityResponse CentralEntity,
    List<RelationshipResponse> Relationships);

public record SimilarEntityResponse(
    EntityResponse Entity,
    double SimilarityScore);

public record PathResponse(
    List<EntityResponse> Entities,
    List<string> RelationshipTypes,
    int Length);

public record PathsResponse(List<PathResponse> Paths);
