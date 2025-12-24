using Aegis.Domain.Entities;
using Aegis.Domain.Repositories;
using Aegis.Infrastructure.Persistence;
using Aegis.Infrastructure.Persistence.Repositories;
using Testcontainers.PostgreSql;

namespace Aegis.IntegrationTests.Repositories;

public class UserRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("pgvector/pgvector:pg17")
        .WithDatabase("aegis_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private string ConnectionString => _postgres.GetConnectionString();
    private IUserRepository _repository = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // Run migrations
        var migrator = new DatabaseMigrator(ConnectionString);
        migrator.Migrate();

        // Create repository
        _repository = new UserRepository(ConnectionString);
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task AddAsync_ShouldCreateUser()
    {
        // Arrange
        var user = User.Create("test@example.com", "Test User");

        // Act
        await _repository.AddAsync(user);

        // Assert
        var retrieved = await _repository.GetByIdAsync(user.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Email.Should().Be("test@example.com");
        retrieved.Name.Should().Be("Test User");
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentId_ShouldReturnNull()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _repository.GetByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByEmailAsync_ShouldReturnUser()
    {
        // Arrange
        var user = User.Create("email@test.com", "Email Test");
        await _repository.AddAsync(user);

        // Act
        var result = await _repository.GetByEmailAsync("email@test.com");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(user.Id);
    }

    [Fact]
    public async Task GetByEmailAsync_WithNonExistentEmail_ShouldReturnNull()
    {
        // Act
        var result = await _repository.GetByEmailAsync("nonexistent@example.com");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_ShouldModifyUser()
    {
        // Arrange
        var user = User.Create("update@test.com", "Original Name");
        await _repository.AddAsync(user);

        // Act
        user.UpdateProfile("Updated Name");
        await _repository.UpdateAsync(user);

        // Assert
        var retrieved = await _repository.GetByIdAsync(user.Id);
        retrieved!.Name.Should().Be("Updated Name");
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveUser()
    {
        // Arrange
        var user = User.Create("delete@test.com", "To Delete");
        await _repository.AddAsync(user);

        // Act
        await _repository.DeleteAsync(user.Id);

        // Assert
        var result = await _repository.GetByIdAsync(user.Id);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllUsers()
    {
        // Arrange
        var user1 = User.Create("user1@test.com", "User One");
        var user2 = User.Create("user2@test.com", "User Two");
        await _repository.AddAsync(user1);
        await _repository.AddAsync(user2);

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        result.Should().HaveCountGreaterOrEqualTo(2);
        result.Should().Contain(u => u.Email == "user1@test.com");
        result.Should().Contain(u => u.Email == "user2@test.com");
    }

    [Fact]
    public async Task ExistsAsync_WithExistingUser_ShouldReturnTrue()
    {
        // Arrange
        var user = User.Create("exists@test.com", "Exists");
        await _repository.AddAsync(user);

        // Act
        var result = await _repository.ExistsAsync(user.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WithNonExistentUser_ShouldReturnFalse()
    {
        // Act
        var result = await _repository.ExistsAsync(Guid.NewGuid());

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task EmailExistsAsync_WithExistingEmail_ShouldReturnTrue()
    {
        // Arrange
        var user = User.Create("emailexists@test.com", "Email Exists");
        await _repository.AddAsync(user);

        // Act
        var result = await _repository.EmailExistsAsync("emailexists@test.com");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task EmailExistsAsync_WithNonExistentEmail_ShouldReturnFalse()
    {
        // Act
        var result = await _repository.EmailExistsAsync("nonexistent@test.com");

        // Assert
        result.Should().BeFalse();
    }
}
