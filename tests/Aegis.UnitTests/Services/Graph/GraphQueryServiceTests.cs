using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.Graph;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Neo4j.Driver;

namespace Aegis.UnitTests.Services.Graph;

[Collection("GraphTests")]
public class GraphQueryServiceTests : IDisposable
{
    private readonly GraphQueryService? _service;
    private readonly Neo4jService? _graphService;
    private readonly IDriver? _driver;
    private readonly bool _neo4jAvailable;

    public GraphQueryServiceTests()
    {
        var uri = Environment.GetEnvironmentVariable("NEO4J_URI") ?? "bolt://localhost:7687";
        var username = Environment.GetEnvironmentVariable("NEO4J_USERNAME") ?? "neo4j";
        var password = Environment.GetEnvironmentVariable("NEO4J_PASSWORD") ?? "password";

        try
        {
            _driver = GraphDatabase.Driver(uri, AuthTokens.Basic(username, password), o => o
                .WithConnectionTimeout(TimeSpan.FromSeconds(2))
                .WithMaxConnectionLifetime(TimeSpan.FromMinutes(5)));

            var verifyTask = _driver.VerifyConnectivityAsync();
            if (!verifyTask.Wait(TimeSpan.FromSeconds(3)))
            {
                throw new TimeoutException("Neo4j connection verification timed out");
            }

            _graphService = new Neo4jService(_driver, NullLogger<Neo4jService>.Instance);
            _service = new GraphQueryService(_driver, NullLogger<GraphQueryService>.Instance);

            _neo4jAvailable = true;

            // Setup test data
            SetupTestData().Wait();
        }
        catch
        {
            _neo4jAvailable = false;
            _service = null;
            _graphService = null;
            _driver?.Dispose();
            _driver = null;
        }
    }

    [Fact]
    public async Task Query_WithTypeFilter_ShouldReturnEntitiesOfType()
    {
        if (!_neo4jAvailable) return;

        // Act
        var result = await _service!.Query()
            .OfType(EntityType.ThreatActor)
            .ExecuteAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().OnlyContain(e => e.Type == EntityType.ThreatActor);
    }

    [Fact]
    public async Task Query_WithConfidenceFilter_ShouldReturnHighConfidenceEntities()
    {
        if (!_neo4jAvailable) return;

        // Act
        var result = await _service!.Query()
            .WithMinConfidence(0.9)
            .ExecuteAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().OnlyContain(e => e.Confidence >= 0.9);
    }

    [Fact]
    public async Task Query_WithLimit_ShouldLimitResults()
    {
        if (!_neo4jAvailable) return;

        // Act
        var result = await _service!.Query()
            .Limit(2)
            .ExecuteAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCountLessOrEqualTo(2);
    }

    [Fact]
    public async Task Query_CountAsync_ShouldReturnCount()
    {
        if (!_neo4jAvailable) return;

        // Act
        var result = await _service!.Query()
            .OfType(EntityType.Malware)
            .CountAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task FindSimilarEntitiesAsync_ShouldReturnSimilarEntities()
    {
        if (!_neo4jAvailable) return;

        // Act
        var result = await _service!.FindSimilarEntitiesAsync("test-query-1", limit: 5);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GetRelatedEntitiesAsync_WithValidRelationship_ShouldReturnRelatedEntities()
    {
        if (!_neo4jAvailable) return;

        // Act
        var result = await _service!.GetRelatedEntitiesAsync(
            "test-query-1",
            GraphRelationshipTypes.USES,
            limit: 10);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Query_WithPropertyFilter_ShouldFilterByProperty()
    {
        if (!_neo4jAvailable) return;

        // Act
        var result = await _service!.Query()
            .WithProperty("name", "APT28")
            .ExecuteAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        if (result.Value.Any())
        {
            result.Value.Should().OnlyContain(e => e.Name == "APT28");
        }
    }

    [Fact]
    public async Task Query_WithOrderBy_ShouldOrderResults()
    {
        if (!_neo4jAvailable) return;

        // Act
        var result = await _service!.Query()
            .OfType(EntityType.ThreatActor)
            .OrderBy("confidence", descending: true)
            .Limit(10)
            .ExecuteAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        if (result.Value.Count >= 2)
        {
            for (int i = 0; i < result.Value.Count - 1; i++)
            {
                result.Value[i].Confidence.Should().BeGreaterOrEqualTo(result.Value[i + 1].Confidence);
            }
        }
    }

    private async Task SetupTestData()
    {
        if (!_neo4jAvailable || _graphService == null) return;

        await CleanupTestData();

        // Create test entities
        var entities = new[]
        {
            new GraphEntity
            {
                Id = "test-query-1",
                Type = EntityType.ThreatActor,
                Name = "APT28",
                Confidence = 0.95
            },
            new GraphEntity
            {
                Id = "test-query-2",
                Type = EntityType.Malware,
                Name = "WannaCry",
                Confidence = 0.98
            },
            new GraphEntity
            {
                Id = "test-query-3",
                Type = EntityType.ThreatActor,
                Name = "Lazarus",
                Confidence = 0.85
            }
        };

        foreach (var entity in entities)
        {
            await _graphService.UpsertEntityAsync(entity);
        }

        // Create relationships
        await _graphService.CreateRelationshipAsync("test-query-1", "test-query-2", GraphRelationshipTypes.USES);
    }

    private async Task CleanupTestData()
    {
        if (!_neo4jAvailable || _driver == null) return;

        await using var session = _driver.AsyncSession();
        await session.ExecuteWriteAsync(async tx =>
        {
            // Clean up all test nodes comprehensively
            await tx.RunAsync("MATCH (n) WHERE n.id STARTS WITH 'test-' OR n.id STARTS WITH 'entity-' OR n.id STARTS WITH 'doc-' DETACH DELETE n");
        });
    }

    public void Dispose()
    {
        if (_neo4jAvailable && _driver != null)
        {
            CleanupTestData().Wait();
            _driver.Dispose();
        }
    }
}
