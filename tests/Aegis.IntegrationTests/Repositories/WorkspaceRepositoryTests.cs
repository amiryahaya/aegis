using Aegis.Domain.Entities;
using Aegis.Domain.Repositories;
using Aegis.Infrastructure.Persistence;
using Aegis.Infrastructure.Persistence.Repositories;
using Testcontainers.PostgreSql;

namespace Aegis.IntegrationTests.Repositories;

public class WorkspaceRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("pgvector/pgvector:pg17")
        .WithDatabase("aegis_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private string ConnectionString => _postgres.GetConnectionString();
    private IWorkspaceRepository _repository = null!;
    private IUserRepository _userRepository = null!;
    private ITeamRepository _teamRepository = null!;
    private User _testUser = null!;
    private Team _testTeam = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var migrator = new DatabaseMigrator(ConnectionString);
        migrator.Migrate();

        _repository = new WorkspaceRepository(ConnectionString);
        _userRepository = new UserRepository(ConnectionString);
        _teamRepository = new TeamRepository(ConnectionString);

        // Create test user and team
        _testUser = User.Create($"{Guid.NewGuid()}@test.com", "Test User");
        await _userRepository.AddAsync(_testUser);

        _testTeam = Team.Create("Test Team", _testUser.Id);
        await _teamRepository.AddAsync(_testTeam);
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task AddAsync_ShouldCreateWorkspace()
    {
        // Arrange
        var workspace = Workspace.Create("Test Workspace", _testUser.Id, "A test workspace", _testTeam.Id);

        // Act
        await _repository.AddAsync(workspace);

        // Assert
        var retrieved = await _repository.GetByIdAsync(workspace.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Test Workspace");
        retrieved.Description.Should().Be("A test workspace");
        retrieved.TeamId.Should().Be(_testTeam.Id);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentId_ShouldReturnNull()
    {
        // Act
        var result = await _repository.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllWorkspaces()
    {
        // Arrange
        var workspace1 = Workspace.Create("Workspace 1", _testUser.Id);
        var workspace2 = Workspace.Create("Workspace 2", _testUser.Id);
        await _repository.AddAsync(workspace1);
        await _repository.AddAsync(workspace2);

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        result.Should().HaveCountGreaterOrEqualTo(2);
        result.Should().Contain(w => w.Name == "Workspace 1");
        result.Should().Contain(w => w.Name == "Workspace 2");
    }

    [Fact]
    public async Task GetByTeamIdAsync_ShouldReturnTeamWorkspaces()
    {
        // Arrange
        var workspace = Workspace.Create("Team Workspace", _testUser.Id, null, _testTeam.Id);
        await _repository.AddAsync(workspace);

        // Act
        var result = await _repository.GetByTeamIdAsync(_testTeam.Id);

        // Assert
        result.Should().HaveCountGreaterOrEqualTo(1);
        result.Should().Contain(w => w.Name == "Team Workspace");
    }

    [Fact]
    public async Task GetByUserIdAsync_ShouldReturnUserWorkspaces()
    {
        // Arrange
        var workspace = Workspace.Create("User Workspace", _testUser.Id);
        await _repository.AddAsync(workspace);

        // Act
        var result = await _repository.GetByUserIdAsync(_testUser.Id);

        // Assert
        result.Should().HaveCountGreaterOrEqualTo(1);
        result.Should().Contain(w => w.Name == "User Workspace");
    }

    [Fact]
    public async Task UpdateAsync_ShouldModifyWorkspace()
    {
        // Arrange
        var workspace = Workspace.Create("Original Name", _testUser.Id);
        await _repository.AddAsync(workspace);

        // Act
        workspace.UpdateDetails("Updated Name", "Updated description");
        await _repository.UpdateAsync(workspace);

        // Assert
        var retrieved = await _repository.GetByIdAsync(workspace.Id);
        retrieved!.Name.Should().Be("Updated Name");
        retrieved.Description.Should().Be("Updated description");
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveWorkspace()
    {
        // Arrange
        var workspace = Workspace.Create("To Delete", _testUser.Id);
        await _repository.AddAsync(workspace);

        // Act
        await _repository.DeleteAsync(workspace.Id);

        // Assert
        var result = await _repository.GetByIdAsync(workspace.Id);
        result.Should().BeNull();
    }

    [Fact]
    public async Task ExistsAsync_WithExistingWorkspace_ShouldReturnTrue()
    {
        // Arrange
        var workspace = Workspace.Create("Exists Workspace", _testUser.Id);
        await _repository.AddAsync(workspace);

        // Act
        var result = await _repository.ExistsAsync(workspace.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WithNonExistentWorkspace_ShouldReturnFalse()
    {
        // Act
        var result = await _repository.ExistsAsync(Guid.NewGuid());

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task NameExistsInTeamAsync_WithExistingName_ShouldReturnTrue()
    {
        // Arrange
        var workspace = Workspace.Create("Unique Workspace", _testUser.Id, null, _testTeam.Id);
        await _repository.AddAsync(workspace);

        // Act
        var result = await _repository.NameExistsInTeamAsync("Unique Workspace", _testTeam.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task NameExistsInTeamAsync_WithNonExistentName_ShouldReturnFalse()
    {
        // Act
        var result = await _repository.NameExistsInTeamAsync("Non Existent", _testTeam.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task Archive_ShouldUpdateStatus()
    {
        // Arrange
        var workspace = Workspace.Create("To Archive", _testUser.Id);
        await _repository.AddAsync(workspace);

        // Act
        workspace.Archive();
        await _repository.UpdateAsync(workspace);

        // Assert
        var retrieved = await _repository.GetByIdAsync(workspace.Id);
        retrieved!.Status.Should().Be(WorkspaceStatus.Archived);
    }
}
