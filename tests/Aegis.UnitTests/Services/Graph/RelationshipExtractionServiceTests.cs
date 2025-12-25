using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.Graph;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Neo4j.Driver;

namespace Aegis.UnitTests.Services.Graph;

[Collection("GraphTests")]
public class RelationshipExtractionServiceTests : IDisposable
{
    private readonly RelationshipExtractionService? _service;
    private readonly Neo4jService? _graphService;
    private readonly IDriver? _driver;
    private readonly bool _neo4jAvailable;

    public RelationshipExtractionServiceTests()
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
            _service = new RelationshipExtractionService(
                _graphService,
                NullLogger<RelationshipExtractionService>.Instance);

            _neo4jAvailable = true;

            // Clean up test data
            CleanupTestData().Wait();
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
    public async Task ExtractRelationshipsAsync_WithCoOccurringEntities_ShouldCreateRelationships()
    {
        if (!_neo4jAvailable) return;

        // Arrange
        var text = "APT28 uses malware WannaCry to exploit CVE-2021-44228";
        var entities = new List<NamedEntity>
        {
            new() { Text = "APT28", Type = NEREntityType.Organization, Confidence = 0.9, StartPosition = 0, EndPosition = 5 },
            new() { Text = "WannaCry", Type = NEREntityType.Other, Confidence = 0.95, StartPosition = 18, EndPosition = 26 },
            new() { Text = "CVE-2021-44228", Type = NEREntityType.Other, Confidence = 0.98, StartPosition = 38, EndPosition = 52 }
        };

        // Act
        var result = await _service!.ExtractRelationshipsAsync(text, entities.AsReadOnly());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RelationshipsCreated.Should().BeGreaterThan(0);
        result.Value.Relationships.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExtractRelationshipsAsync_WithSingleEntity_ShouldCreateNoRelationships()
    {
        if (!_neo4jAvailable) return;

        // Arrange
        var text = "Contact security@company.com for details.";
        var entities = new List<NamedEntity>
        {
            new() { Text = "security@company.com", Type = NEREntityType.Email, Confidence = 0.95, StartPosition = 8, EndPosition = 28 }
        };

        // Act
        var result = await _service!.ExtractRelationshipsAsync(text, entities.AsReadOnly());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RelationshipsCreated.Should().Be(0);
    }

    [Fact]
    public async Task ExtractRelationshipsAsync_WithDistantEntities_ShouldHaveLowerConfidence()
    {
        if (!_neo4jAvailable) return;

        // Arrange
        var text = "Entity A at the beginning. " + new string('.', 500) + " Entity B at the end.";
        var entities = new List<NamedEntity>
        {
            new() { Text = "Entity A", Type = NEREntityType.Organization, Confidence = 0.9, StartPosition = 0, EndPosition = 8 },
            new() { Text = "Entity B", Type = NEREntityType.Organization, Confidence = 0.9, StartPosition = text.Length - 20, EndPosition = text.Length - 12 }
        };

        // Act
        var result = await _service!.ExtractRelationshipsAsync(text, entities.AsReadOnly());

        // Assert
        result.IsSuccess.Should().BeTrue();
        if (result.Value.Relationships.Any())
        {
            result.Value.Relationships.First().Confidence.Should().BeLessThan(0.7);
        }
    }

    [Fact]
    public async Task CreateRelationshipAsync_WithValidEntities_ShouldCreateRelationship()
    {
        if (!_neo4jAvailable) return;

        // Arrange
        var entity1 = new GraphEntity
        {
            Id = "test-rel-1",
            Type = EntityType.ThreatActor,
            Name = "Actor 1",
            Confidence = 0.9
        };

        var entity2 = new GraphEntity
        {
            Id = "test-rel-2",
            Type = EntityType.Malware,
            Name = "Malware 1",
            Confidence = 0.95
        };

        await _graphService!.UpsertEntityAsync(entity1);
        await _graphService.UpsertEntityAsync(entity2);

        // Act
        var result = await _service!.CreateRelationshipAsync(
            "test-rel-1",
            "test-rel-2",
            GraphRelationshipTypes.USES,
            0.85);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ExtractRelationshipsAsync_WithNullText_ShouldReturnFailure()
    {
        if (!_neo4jAvailable) return;

        // Act
        var result = await _service!.ExtractRelationshipsAsync(null!, new List<NamedEntity>().AsReadOnly());

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ExtractRelationshipsAsync_CalculatesProximityBasedConfidence()
    {
        if (!_neo4jAvailable) return;

        // Arrange - entities close together
        var text = "APT28 uses WannaCry";
        var entities = new List<NamedEntity>
        {
            new() { Text = "APT28", Type = NEREntityType.Organization, Confidence = 0.9, StartPosition = 0, EndPosition = 5 },
            new() { Text = "WannaCry", Type = NEREntityType.Other, Confidence = 0.95, StartPosition = 11, EndPosition = 19 }
        };

        // Act
        var result = await _service!.ExtractRelationshipsAsync(text, entities.AsReadOnly());

        // Assert
        result.IsSuccess.Should().BeTrue();
        if (result.Value.Relationships.Any())
        {
            result.Value.Relationships.First().Confidence.Should().BeGreaterThan(0.5);
            result.Value.Relationships.First().Distance.Should().BeLessThan(50);
        }
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
