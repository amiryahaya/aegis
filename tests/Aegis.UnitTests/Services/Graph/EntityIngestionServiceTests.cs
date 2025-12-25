using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.Graph;
using Aegis.Infrastructure.Services.NER;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Neo4j.Driver;

namespace Aegis.UnitTests.Services.Graph;

[Collection("GraphTests")]
public class EntityIngestionServiceTests : IDisposable
{
    private readonly EntityIngestionService? _service;
    private readonly IDriver? _driver;
    private readonly bool _neo4jAvailable;

    public EntityIngestionServiceTests()
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

            var graphService = new Neo4jService(_driver, NullLogger<Neo4jService>.Instance);
            var basicNER = new BasicNERService(NullLogger<BasicNERService>.Instance);
            var nerService = new IntelligenceNERService(basicNER, NullLogger<IntelligenceNERService>.Instance);

            _service = new EntityIngestionService(
                graphService,
                nerService,
                NullLogger<EntityIngestionService>.Instance);

            _neo4jAvailable = true;

            // Clean up test data
            CleanupTestData().Wait();
        }
        catch
        {
            _neo4jAvailable = false;
            _service = null;
            _driver?.Dispose();
            _driver = null;
        }
    }

    [Fact]
    public async Task IngestFromTextAsync_WithThreatIntelligence_ShouldExtractAndIngestEntities()
    {
        if (!_neo4jAvailable) return;

        // Arrange
        var text = @"
            The threat actor APT28 has been observed using the malware WannaCry
            to exploit CVE-2021-44228 (Log4Shell) vulnerability.
            Contact security@company.com for more details.
        ";

        // Act
        var result = await _service!.IngestFromTextAsync(text, "doc-123");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EntitiesIngested.Should().BeGreaterThan(0);
        result.Value.EntityIds.Should().NotBeEmpty();
    }

    [Fact]
    public async Task IngestFromTextAsync_WithNoEntities_ShouldReturnZeroCount()
    {
        if (!_neo4jAvailable) return;

        // Arrange
        var text = "This is plain text with no recognizable entities.";

        // Act
        var result = await _service!.IngestFromTextAsync(text, "doc-456");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EntitiesIngested.Should().Be(0);
    }

    [Fact]
    public async Task IngestEntityAsync_WithNewEntity_ShouldCreateEntity()
    {
        if (!_neo4jAvailable) return;

        // Arrange
        var entity = new GraphEntity
        {
            Id = "test-ingestion-1",
            Type = EntityType.ThreatActor,
            Name = "Lazarus Group",
            Properties = new Dictionary<string, object>
            {
                ["description"] = "North Korean APT group"
            },
            Confidence = 0.95
        };

        // Act
        var result = await _service!.IngestEntityAsync(entity);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be("test-ingestion-1");
        result.Value.Name.Should().Be("Lazarus Group");
    }

    [Fact]
    public async Task IngestEntityAsync_WithDuplicateEntity_ShouldUpdateExisting()
    {
        if (!_neo4jAvailable) return;

        // Arrange
        var entity1 = new GraphEntity
        {
            Id = "test-ingestion-2",
            Type = EntityType.Malware,
            Name = "Original Name",
            Confidence = 0.7
        };

        var entity2 = new GraphEntity
        {
            Id = "test-ingestion-2",
            Type = EntityType.Malware,
            Name = "Updated Name",
            Confidence = 0.9
        };

        // Act
        await _service!.IngestEntityAsync(entity1);
        var result = await _service.IngestEntityAsync(entity2);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Updated Name");
        result.Value.Confidence.Should().Be(0.9);
    }

    [Fact]
    public async Task IngestFromTextAsync_WithMultipleEntityTypes_ShouldIngestAll()
    {
        if (!_neo4jAvailable) return;

        // Arrange
        var text = @"
            Malware hash: 5d41402abc4b2a76b9719d911017c592
            Contact: analyst@security.org
            IP Address: 192.168.1.100
            CVE-2023-12345 vulnerability discovered
        ";

        // Act
        var result = await _service!.IngestFromTextAsync(text, "doc-789");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EntitiesIngested.Should().BeGreaterOrEqualTo(4);
    }

    [Fact]
    public async Task IngestFromTextAsync_WithInvalidText_ShouldReturnFailure()
    {
        if (!_neo4jAvailable) return;

        // Act
        var result = await _service!.IngestFromTextAsync(null!, "doc-999");

        // Assert
        result.IsFailure.Should().BeTrue();
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
