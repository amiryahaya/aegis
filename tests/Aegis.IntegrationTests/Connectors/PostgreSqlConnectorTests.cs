using Aegis.Domain.Entities;
using Aegis.Domain.Repositories;
using Aegis.Domain.Services;
using Aegis.Infrastructure.Persistence;
using Aegis.Infrastructure.Persistence.Repositories;
using Aegis.Infrastructure.Services.Connectors;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Testcontainers.PostgreSql;

namespace Aegis.IntegrationTests.Connectors;

public class PostgreSqlConnectorTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _sourceDatabase = new PostgreSqlBuilder()
        .WithImage("postgres:17")
        .WithDatabase("source_db")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly PostgreSqlContainer _aegisDatabase = new PostgreSqlBuilder()
        .WithImage("pgvector/pgvector:pg17")
        .WithDatabase("aegis_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private string SourceConnectionString => _sourceDatabase.GetConnectionString();
    private string AegisConnectionString => _aegisDatabase.GetConnectionString();
    private IDocumentRepository _documentRepository = null!;
    private IDataConnector _connector = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(
            _sourceDatabase.StartAsync(),
            _aegisDatabase.StartAsync());

        // Run AEGIS migrations
        var migrator = new DatabaseMigrator(AegisConnectionString);
        migrator.Migrate();

        // Create repository and connector
        _documentRepository = new DocumentRepository(AegisConnectionString);
        var logger = Substitute.For<ILogger<PostgreSqlConnector>>();
        _connector = new PostgreSqlConnector(_documentRepository, logger);

        // Set up source database with test data
        await SetupSourceDatabase();
    }

    public async Task DisposeAsync()
    {
        await Task.WhenAll(
            _sourceDatabase.DisposeAsync().AsTask(),
            _aegisDatabase.DisposeAsync().AsTask());
    }

    private async Task SetupSourceDatabase()
    {
        await using var connection = new Npgsql.NpgsqlConnection(SourceConnectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE products (
                id SERIAL PRIMARY KEY,
                name VARCHAR(255) NOT NULL,
                description TEXT,
                price DECIMAL(10, 2),
                created_at TIMESTAMP DEFAULT NOW(),
                updated_at TIMESTAMP
            );

            INSERT INTO products (name, description, price, created_at, updated_at)
            VALUES
                ('Product 1', 'Description 1', 99.99, NOW() - INTERVAL '2 days', NOW() - INTERVAL '2 days'),
                ('Product 2', 'Description 2', 149.99, NOW() - INTERVAL '1 day', NOW() - INTERVAL '1 day'),
                ('Product 3', 'Description 3', 199.99, NOW(), NOW());
        ";
        await cmd.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task ValidateSettingsAsync_WithValidSettings_ShouldSucceed()
    {
        // Arrange
        var settings = new Dictionary<string, string>
        {
            ["host"] = "localhost",
            ["port"] = "5432",
            ["database"] = "source_db",
            ["username"] = "postgres",
            ["password"] = "postgres",
            ["table"] = "products",
            ["primary_key_column"] = "id"
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
            ["host"] = "localhost"
            // Missing other required settings
        };

        // Act
        var result = await _connector.ValidateSettingsAsync(settings);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error!.Message.Should().Contain("Missing required settings");
    }

    [Fact]
    public async Task TestConnectionAsync_WithValidCredentials_ShouldSucceed()
    {
        // Arrange
        var settings = new Dictionary<string, string>
        {
            ["host"] = _sourceDatabase.Hostname,
            ["port"] = _sourceDatabase.GetMappedPublicPort(5432).ToString(),
            ["database"] = "source_db",
            ["username"] = "postgres",
            ["password"] = "postgres",
            ["table"] = "products",
            ["primary_key_column"] = "id"
        };

        // Act
        var result = await _connector.TestConnectionAsync(settings);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.IsSuccessful.Should().BeTrue();
        result.Value.Message.Should().Contain("Successfully connected");
    }

    [Fact]
    public async Task TestConnectionAsync_WithInvalidCredentials_ShouldFail()
    {
        // Arrange
        var settings = new Dictionary<string, string>
        {
            ["host"] = "localhost",
            ["port"] = "9999",
            ["database"] = "invalid",
            ["username"] = "invalid",
            ["password"] = "invalid",
            ["table"] = "products",
            ["primary_key_column"] = "id"
        };

        // Act
        var result = await _connector.TestConnectionAsync(settings);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SyncAsync_WithFullSync_ShouldSyncAllRecords()
    {
        // Arrange
        var dataSourceId = Guid.NewGuid();
        var settings = new Dictionary<string, string>
        {
            ["host"] = _sourceDatabase.Hostname,
            ["port"] = _sourceDatabase.GetMappedPublicPort(5432).ToString(),
            ["database"] = "source_db",
            ["username"] = "postgres",
            ["password"] = "postgres",
            ["table"] = "products",
            ["primary_key_column"] = "id"
        };

        var syncOptions = new SyncOptions(
            IsFullSync: true,
            LastSyncCursor: null,
            BatchSize: 100);

        // Act
        var result = await _connector.SyncAsync(dataSourceId, settings, syncOptions);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.DocumentsAdded.Should().Be(3);
        result.Value.DocumentsFailed.Should().Be(0);
    }

    [Fact]
    public async Task SyncAsync_WithIncrementalSync_ShouldSyncOnlyNewRecords()
    {
        // Arrange - First do a full sync
        var dataSourceId = Guid.NewGuid();
        var settings = new Dictionary<string, string>
        {
            ["host"] = _sourceDatabase.Hostname,
            ["port"] = _sourceDatabase.GetMappedPublicPort(5432).ToString(),
            ["database"] = "source_db",
            ["username"] = "postgres",
            ["password"] = "postgres",
            ["table"] = "products",
            ["primary_key_column"] = "id",
            ["timestamp_column"] = "updated_at"
        };

        var fullSyncOptions = new SyncOptions(IsFullSync: true, LastSyncCursor: null);
        await _connector.SyncAsync(dataSourceId, settings, fullSyncOptions);

        // Add a new record to source database
        await using (var connection = new Npgsql.NpgsqlConnection(SourceConnectionString))
        {
            await connection.OpenAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT INTO products (name, description, price, updated_at) VALUES ('Product 4', 'Description 4', 249.99, NOW())";
            await cmd.ExecuteNonQueryAsync();
        }

        // Act - Do incremental sync
        var incrementalOptions = new SyncOptions(IsFullSync: false, LastSyncCursor: DateTime.UtcNow.AddSeconds(-10).ToString("O"));
        var result = await _connector.SyncAsync(dataSourceId, settings, incrementalOptions);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.DocumentsAdded.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SyncAsync_WithInvalidTable_ShouldFail()
    {
        // Arrange
        var dataSourceId = Guid.NewGuid();
        var settings = new Dictionary<string, string>
        {
            ["host"] = _sourceDatabase.Hostname,
            ["port"] = _sourceDatabase.GetMappedPublicPort(5432).ToString(),
            ["database"] = "source_db",
            ["username"] = "postgres",
            ["password"] = "postgres",
            ["table"] = "nonexistent_table",
            ["primary_key_column"] = "id"
        };

        var syncOptions = new SyncOptions(IsFullSync: true, LastSyncCursor: null);

        // Act
        var result = await _connector.SyncAsync(dataSourceId, settings, syncOptions);

        // Assert
        result.IsFailure.Should().BeTrue();
    }
}
