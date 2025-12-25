using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.Graph;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Neo4j.Driver;

namespace Aegis.UnitTests.Services.Graph;

[Collection("GraphTests")]
public class Neo4jServiceTests : IDisposable
{
    private readonly Neo4jService _service;
    private readonly IDriver? _driver;
    private readonly bool _neo4jAvailable;

    public Neo4jServiceTests()
    {
        // Try to connect to Neo4j for integration testing
        // If not available, tests will be skipped
        var uri = Environment.GetEnvironmentVariable("NEO4J_URI") ?? "bolt://localhost:7687";
        var username = Environment.GetEnvironmentVariable("NEO4J_USERNAME") ?? "neo4j";
        var password = Environment.GetEnvironmentVariable("NEO4J_PASSWORD") ?? "password";

        try
        {
            // Configure driver with connection timeout
            _driver = GraphDatabase.Driver(uri, AuthTokens.Basic(username, password), o => o
                .WithConnectionTimeout(TimeSpan.FromSeconds(2))
                .WithMaxConnectionLifetime(TimeSpan.FromMinutes(5)));

            // Verify connection with timeout
            var verifyTask = _driver.VerifyConnectivityAsync();
            if (!verifyTask.Wait(TimeSpan.FromSeconds(3)))
            {
                throw new TimeoutException("Neo4j connection verification timed out");
            }

            _service = new Neo4jService(_driver, NullLogger<Neo4jService>.Instance);
            _neo4jAvailable = true;

            // Clean up test data
            CleanupTestData().Wait();
        }
        catch
        {
            _neo4jAvailable = false;
            _service = null!;
            _driver?.Dispose();
            _driver = null;
        }
    }

    [Fact]
    public async Task UpsertEntityAsync_WithValidEntity_ShouldCreateEntity()
    {
        if (!_neo4jAvailable) return;

        // Arrange
        var entity = new GraphEntity
        {
            Id = "test-entity-1",
            Type = EntityType.ThreatActor,
            Name = "APT28",
            Properties = new Dictionary<string, object>
            {
                ["description"] = "Russian state-sponsored threat group",
                ["aliases"] = "Fancy Bear, Sofacy"
            },
            Confidence = 0.95
        };

        // Act
        var result = await _service.UpsertEntityAsync(entity);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be("test-entity-1");
        result.Value.Name.Should().Be("APT28");
        result.Value.Type.Should().Be(EntityType.ThreatActor);
    }

    [Fact]
    public async Task CreateRelationshipAsync_BetweenEntities_ShouldCreateRelationship()
    {
        if (!_neo4jAvailable) return;

        // Arrange
        var threatActor = new GraphEntity
        {
            Id = "test-actor-1",
            Type = EntityType.ThreatActor,
            Name = "Lazarus Group",
            Confidence = 0.9
        };

        var malware = new GraphEntity
        {
            Id = "test-malware-1",
            Type = EntityType.Malware,
            Name = "WannaCry",
            Confidence = 0.95
        };

        await _service.UpsertEntityAsync(threatActor);
        await _service.UpsertEntityAsync(malware);

        // Act
        var result = await _service.CreateRelationshipAsync(
            "test-actor-1",
            "test-malware-1",
            "USES",
            new Dictionary<string, object> { ["since"] = "2017" });

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GetEntityNetworkAsync_WithExistingEntity_ShouldReturnNetwork()
    {
        if (!_neo4jAvailable) return;

        // Arrange
        var centralEntity = new GraphEntity
        {
            Id = "test-central-1",
            Type = EntityType.Vulnerability,
            Name = "CVE-2021-44228",
            Properties = new Dictionary<string, object>
            {
                ["severity"] = "Critical",
                ["cvss"] = 10.0
            },
            Confidence = 1.0
        };

        var relatedMalware = new GraphEntity
        {
            Id = "test-malware-2",
            Type = EntityType.Malware,
            Name = "Log4Shell Exploit Kit",
            Confidence = 0.9
        };

        await _service.UpsertEntityAsync(centralEntity);
        await _service.UpsertEntityAsync(relatedMalware);
        await _service.CreateRelationshipAsync("test-malware-2", "test-central-1", "EXPLOITS");

        // Act
        var result = await _service.GetEntityNetworkAsync("test-central-1", maxHops: 1);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Entity.Name.Should().Be("CVE-2021-44228");
        result.Value.Relationships.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SearchEntitiesAsync_ByType_ShouldReturnMatchingEntities()
    {
        if (!_neo4jAvailable) return;

        // Arrange
        await _service.UpsertEntityAsync(new GraphEntity
        {
            Id = "test-malware-3",
            Type = EntityType.Malware,
            Name = "Emotet",
            Confidence = 0.95
        });

        await _service.UpsertEntityAsync(new GraphEntity
        {
            Id = "test-malware-4",
            Type = EntityType.Malware,
            Name = "TrickBot",
            Confidence = 0.9
        });

        // Act
        var result = await _service.SearchEntitiesAsync(entityType: EntityType.Malware, limit: 10);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        result.Value.Should().OnlyContain(e => e.Type == EntityType.Malware);
    }

    [Fact]
    public async Task FindPathsAsync_BetweenConnectedEntities_ShouldReturnPaths()
    {
        if (!_neo4jAvailable) return;

        // Arrange - Create a chain: Actor -> Tool -> Malware
        var actor = new GraphEntity { Id = "test-actor-2", Type = EntityType.ThreatActor, Name = "Actor A", Confidence = 0.9 };
        var tool = new GraphEntity { Id = "test-tool-1", Type = EntityType.Tool, Name = "Tool B", Confidence = 0.8 };
        var malware = new GraphEntity { Id = "test-malware-5", Type = EntityType.Malware, Name = "Malware C", Confidence = 0.85 };

        await _service.UpsertEntityAsync(actor);
        await _service.UpsertEntityAsync(tool);
        await _service.UpsertEntityAsync(malware);

        await _service.CreateRelationshipAsync("test-actor-2", "test-tool-1", "USES");
        await _service.CreateRelationshipAsync("test-tool-1", "test-malware-5", "DEPLOYS");

        // Act
        var result = await _service.FindPathsAsync("test-actor-2", "test-malware-5", maxDepth: 3);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        result.Value.First().Length.Should().Be(2); // 2 hops
    }

    [Fact]
    public async Task DeleteEntityAsync_WithExistingEntity_ShouldRemoveEntity()
    {
        if (!_neo4jAvailable) return;

        // Arrange
        var entity = new GraphEntity
        {
            Id = "test-delete-1",
            Type = EntityType.Infrastructure,
            Name = "Command & Control Server",
            Confidence = 0.8
        };

        await _service.UpsertEntityAsync(entity);

        // Act
        var deleteResult = await _service.DeleteEntityAsync("test-delete-1");

        // Assert
        deleteResult.IsSuccess.Should().BeTrue();

        // Verify deletion
        var searchResult = await _service.SearchEntitiesAsync(
            properties: new Dictionary<string, object> { ["id"] = "test-delete-1" });
        searchResult.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task UpsertEntityAsync_UpdateExisting_ShouldUpdateProperties()
    {
        if (!_neo4jAvailable) return;

        // Arrange
        var originalEntity = new GraphEntity
        {
            Id = "test-update-1",
            Type = EntityType.Campaign,
            Name = "Operation X",
            Properties = new Dictionary<string, object> { ["status"] = "active" },
            Confidence = 0.7
        };

        await _service.UpsertEntityAsync(originalEntity);

        // Act - Update with new properties
        var updatedEntity = new GraphEntity
        {
            Id = "test-update-1",
            Type = EntityType.Campaign,
            Name = "Operation X - Updated",
            Properties = new Dictionary<string, object> { ["status"] = "inactive" },
            Confidence = 0.9
        };

        var result = await _service.UpsertEntityAsync(updatedEntity);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Operation X - Updated");
        result.Value.Confidence.Should().Be(0.9);
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
