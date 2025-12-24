using Aegis.Infrastructure.Persistence;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Aegis.IntegrationTests.Infrastructure;

public class DatabaseMigrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("pgvector/pgvector:pg17")
        .WithDatabase("aegis_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }

    [Fact]
    public void RunMigrations_ShouldSucceed()
    {
        // Arrange
        var migrator = new DatabaseMigrator(ConnectionString);

        // Act
        var result = migrator.Migrate();

        // Assert
        result.Successful.Should().BeTrue(result.Error?.Message ?? "Migration failed");
    }

    [Fact]
    public async Task RunMigrations_ShouldCreateUsersTable()
    {
        // Arrange
        var migrator = new DatabaseMigrator(ConnectionString);

        // Act
        migrator.Migrate();

        // Assert
        var tableExists = await TableExistsAsync("users");
        tableExists.Should().BeTrue("users table should exist after migration");
    }

    [Fact]
    public async Task RunMigrations_ShouldCreateTeamsTable()
    {
        // Arrange
        var migrator = new DatabaseMigrator(ConnectionString);

        // Act
        migrator.Migrate();

        // Assert
        var tableExists = await TableExistsAsync("teams");
        tableExists.Should().BeTrue("teams table should exist after migration");
    }

    [Fact]
    public async Task RunMigrations_ShouldCreateWorkspacesTable()
    {
        // Arrange
        var migrator = new DatabaseMigrator(ConnectionString);

        // Act
        migrator.Migrate();

        // Assert
        var tableExists = await TableExistsAsync("workspaces");
        tableExists.Should().BeTrue("workspaces table should exist after migration");
    }

    [Fact]
    public async Task RunMigrations_ShouldBeIdempotent()
    {
        // Arrange
        var migrator = new DatabaseMigrator(ConnectionString);

        // Act - Run migrations twice
        var firstRun = migrator.Migrate();
        var secondRun = migrator.Migrate();

        // Assert - Both should succeed
        firstRun.Successful.Should().BeTrue();
        secondRun.Successful.Should().BeTrue();

        // Verify table still exists and is intact
        var tableExists = await TableExistsAsync("users");
        tableExists.Should().BeTrue();
    }

    private async Task<bool> TableExistsAsync(string tableName)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_schema = 'public' AND table_name = @tableName)",
            connection);
        cmd.Parameters.AddWithValue("tableName", tableName);
        var result = await cmd.ExecuteScalarAsync();
        return result is true;
    }
}
