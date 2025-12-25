using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.Graph;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Neo4j.Driver;

namespace Aegis.UnitTests.Services.Graph;

[Collection("GraphTests")]
public class GraphSchemaServiceTests : IDisposable
{
    private readonly GraphSchemaService? _service;
    private readonly IDriver? _driver;
    private readonly bool _neo4jAvailable;

    public GraphSchemaServiceTests()
    {
        // Try to connect to Neo4j for integration testing
        var uri = Environment.GetEnvironmentVariable("NEO4J_URI") ?? "bolt://localhost:7687";
        var username = Environment.GetEnvironmentVariable("NEO4J_USERNAME") ?? "neo4j";
        var password = Environment.GetEnvironmentVariable("NEO4J_PASSWORD") ?? "password";

        try
        {
            _driver = GraphDatabase.Driver(uri, AuthTokens.Basic(username, password), o => o
                .WithConnectionTimeout(TimeSpan.FromSeconds(2))
                .WithMaxConnectionLifetime(TimeSpan.FromMinutes(5)));

            // Verify connection with timeout
            var verifyTask = _driver.VerifyConnectivityAsync();
            if (!verifyTask.Wait(TimeSpan.FromSeconds(3)))
            {
                throw new TimeoutException("Neo4j connection verification timed out");
            }

            _service = new GraphSchemaService(_driver, NullLogger<GraphSchemaService>.Instance);
            _neo4jAvailable = true;
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
    public async Task InitializeSchemaAsync_ShouldCreateIndexes()
    {
        if (!_neo4jAvailable) return;

        // Act
        var result = await _service!.InitializeSchemaAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeSchemaAsync_WhenCalledMultipleTimes_ShouldBeIdempotent()
    {
        if (!_neo4jAvailable) return;

        // Act
        var result1 = await _service!.InitializeSchemaAsync();
        var result2 = await _service!.InitializeSchemaAsync();

        // Assert
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task VerifySchemaAsync_AfterInitialization_ShouldReturnTrue()
    {
        if (!_neo4jAvailable) return;

        // Arrange
        await _service!.InitializeSchemaAsync();

        // Act
        var result = await _service.VerifySchemaAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeSchemaAsync_ShouldCreateUniqueConstraintOnEntityId()
    {
        if (!_neo4jAvailable) return;

        // Act
        await _service!.InitializeSchemaAsync();

        // Assert - Try to create duplicate entities
        await using var session = _driver!.AsyncSession();
        var canCreateDuplicate = true;

        try
        {
            await session.ExecuteWriteAsync(async tx =>
            {
                await tx.RunAsync("CREATE (e:ThreatActor {id: 'test-duplicate-id', name: 'Test1'})");
                await tx.RunAsync("CREATE (e:ThreatActor {id: 'test-duplicate-id', name: 'Test2'})");
            });
        }
        catch
        {
            canCreateDuplicate = false;
        }
        finally
        {
            // Cleanup
            await session.ExecuteWriteAsync(async tx =>
            {
                await tx.RunAsync("MATCH (e {id: 'test-duplicate-id'}) DELETE e");
            });
        }

        canCreateDuplicate.Should().BeFalse("unique constraint should prevent duplicate IDs");
    }

    [Fact]
    public async Task InitializeSchemaAsync_ShouldCreateIndexOnEntityName()
    {
        if (!_neo4jAvailable) return;

        // Act
        await _service!.InitializeSchemaAsync();

        // Assert - Verify index exists by checking schema
        await using var session = _driver!.AsyncSession();
        var indexes = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync("SHOW INDEXES");
            var results = new List<string>();
            await foreach (var record in cursor)
            {
                var name = Neo4j.Driver.ValueExtensions.As<string>(record["name"]);
                results.Add(name);
            }
            return results;
        });

        indexes.Should().Contain(i => i.Contains("name") || i.Contains("Name"));
    }

    public void Dispose()
    {
        _driver?.Dispose();
    }
}
