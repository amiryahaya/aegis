using Aegis.Domain.Repositories;
using Aegis.Domain.Services;
using Aegis.Infrastructure.Persistence;
using Aegis.Infrastructure.Persistence.Repositories;
using Aegis.Infrastructure.Services.Connectors;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using NSubstitute;
using Testcontainers.MongoDb;
using Testcontainers.PostgreSql;

namespace Aegis.IntegrationTests.Connectors;

public class MongoDbConnectorTests : IAsyncLifetime
{
    private readonly MongoDbContainer _mongoContainer = new MongoDbBuilder()
        .WithImage("mongo:8.0")
        .Build();

    private readonly PostgreSqlContainer _aegisDatabase = new PostgreSqlBuilder()
        .WithImage("pgvector/pgvector:pg17")
        .WithDatabase("aegis_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private string MongoConnectionString => _mongoContainer.GetConnectionString();
    private string AegisConnectionString => _aegisDatabase.GetConnectionString();
    private IMongoDatabase? _database;
    private IDocumentRepository _documentRepository = null!;
    private IDataConnector _connector = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(
            _mongoContainer.StartAsync(),
            _aegisDatabase.StartAsync());

        // Run AEGIS migrations
        var migrator = new DatabaseMigrator(AegisConnectionString);
        migrator.Migrate();

        // Create repository and connector
        _documentRepository = new DocumentRepository(AegisConnectionString);
        var logger = Substitute.For<ILogger<MongoDbConnector>>();
        _connector = new MongoDbConnector(_documentRepository, logger);

        // Set up test MongoDB database
        var client = new MongoClient(MongoConnectionString);
        _database = client.GetDatabase("test_db");

        var collection = _database.GetCollection<BsonDocument>("products");
        await collection.InsertManyAsync(new[]
        {
            new BsonDocument
            {
                ["_id"] = ObjectId.GenerateNewId(),
                ["name"] = "MongoDB Product 1",
                ["description"] = "First MongoDB product",
                ["price"] = 99.99,
                ["category"] = "Electronics",
                ["createdAt"] = DateTime.UtcNow.AddDays(-2),
                ["updatedAt"] = DateTime.UtcNow.AddDays(-2)
            },
            new BsonDocument
            {
                ["_id"] = ObjectId.GenerateNewId(),
                ["name"] = "MongoDB Product 2",
                ["description"] = "Second MongoDB product",
                ["price"] = 149.99,
                ["category"] = "Books",
                ["createdAt"] = DateTime.UtcNow.AddDays(-1),
                ["updatedAt"] = DateTime.UtcNow.AddDays(-1)
            },
            new BsonDocument
            {
                ["_id"] = ObjectId.GenerateNewId(),
                ["name"] = "MongoDB Product 3",
                ["description"] = "Third MongoDB product",
                ["price"] = 199.99,
                ["category"] = "Electronics",
                ["createdAt"] = DateTime.UtcNow,
                ["updatedAt"] = DateTime.UtcNow
            }
        });
    }

    public async Task DisposeAsync()
    {
        await Task.WhenAll(
            _mongoContainer.DisposeAsync().AsTask(),
            _aegisDatabase.DisposeAsync().AsTask());
    }

    [Fact]
    public async Task ValidateSettingsAsync_WithValidSettings_ShouldSucceed()
    {
        // Arrange
        var settings = new Dictionary<string, string>
        {
            ["connection_string"] = MongoConnectionString,
            ["database"] = "test_db",
            ["collection"] = "products"
        };

        // Act
        var result = await _connector.ValidateSettingsAsync(settings);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateSettingsAsync_WithMissingSettings_ShouldFail()
    {
        // Arrange
        var settings = new Dictionary<string, string>
        {
            ["connection_string"] = MongoConnectionString
            // Missing database and collection
        };

        // Act
        var result = await _connector.ValidateSettingsAsync(settings);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public async Task TestConnectionAsync_WithValidCredentials_ShouldSucceed()
    {
        // Arrange
        var settings = new Dictionary<string, string>
        {
            ["connection_string"] = MongoConnectionString,
            ["database"] = "test_db",
            ["collection"] = "products"
        };

        // Act
        var result = await _connector.TestConnectionAsync(settings);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public async Task TestConnectionAsync_WithInvalidDatabase_ShouldFail()
    {
        // Arrange
        var settings = new Dictionary<string, string>
        {
            ["connection_string"] = "mongodb://localhost:27099",
            ["database"] = "invalid_db",
            ["collection"] = "products"
        };

        // Act
        var result = await _connector.TestConnectionAsync(settings);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SyncAsync_WithFullSync_ShouldSyncAllDocuments()
    {
        // Arrange
        var dataSourceId = Guid.NewGuid();
        var settings = new Dictionary<string, string>
        {
            ["connection_string"] = MongoConnectionString,
            ["database"] = "test_db",
            ["collection"] = "products"
        };

        var syncOptions = new SyncOptions(IsFullSync: true, LastSyncCursor: null);

        // Act
        var result = await _connector.SyncAsync(dataSourceId, settings, syncOptions);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.DocumentsAdded.Should().Be(3);
        result.Value.DocumentsFailed.Should().Be(0);
    }

    [Fact]
    public async Task SyncAsync_WithIncrementalSync_ShouldSyncOnlyNewDocuments()
    {
        // Arrange - First do a full sync
        var dataSourceId = Guid.NewGuid();
        var settings = new Dictionary<string, string>
        {
            ["connection_string"] = MongoConnectionString,
            ["database"] = "test_db",
            ["collection"] = "products",
            ["timestamp_field"] = "updatedAt"
        };

        var fullSyncOptions = new SyncOptions(IsFullSync: true, LastSyncCursor: null);
        await _connector.SyncAsync(dataSourceId, settings, fullSyncOptions);

        // Add a new document
        var collection = _database!.GetCollection<BsonDocument>("products");
        await collection.InsertOneAsync(new BsonDocument
        {
            ["_id"] = ObjectId.GenerateNewId(),
            ["name"] = "MongoDB Product 4",
            ["description"] = "Fourth MongoDB product",
            ["updatedAt"] = DateTime.UtcNow
        });

        // Act - Do incremental sync
        var incrementalOptions = new SyncOptions(IsFullSync: false, LastSyncCursor: DateTime.UtcNow.AddSeconds(-10).ToString("O"));
        var result = await _connector.SyncAsync(dataSourceId, settings, incrementalOptions);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.DocumentsAdded.Should().BeGreaterThan(0);
    }
}
