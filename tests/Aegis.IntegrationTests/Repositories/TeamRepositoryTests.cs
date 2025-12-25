using Aegis.Domain.Entities;
using Aegis.Domain.Repositories;
using Aegis.Infrastructure.Persistence;
using Aegis.Infrastructure.Persistence.Repositories;
using Testcontainers.PostgreSql;

namespace Aegis.IntegrationTests.Repositories;

public class TeamRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("pgvector/pgvector:pg17")
        .WithDatabase("aegis_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private string ConnectionString => _postgres.GetConnectionString();
    private ITeamRepository _repository = null!;
    private IUserRepository _userRepository = null!;
    private User _testUser = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var migrator = new DatabaseMigrator(ConnectionString);
        migrator.Migrate();

        _repository = new TeamRepository(ConnectionString);
        _userRepository = new UserRepository(ConnectionString);

        // Create a test user for team ownership
        _testUser = User.Create($"{Guid.NewGuid()}@test.com", "Test User");
        await _userRepository.AddAsync(_testUser);
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task AddAsync_ShouldCreateTeam()
    {
        // Arrange
        var team = Team.Create("Test Team", _testUser.Id, "A test team");

        // Act
        await _repository.AddAsync(team);

        // Assert
        var retrieved = await _repository.GetByIdAsync(team.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Test Team");
        retrieved.Description.Should().Be("A test team");
        retrieved.CreatedBy.Should().Be(_testUser.Id);
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
    public async Task GetByIdWithMembersAsync_ShouldReturnTeamWithMembers()
    {
        // Arrange
        var team = Team.Create("Team With Members", _testUser.Id);
        await _repository.AddAsync(team);

        var member = TeamMember.Create(team.Id, _testUser.Id, TeamRole.Owner);
        await _repository.AddMemberAsync(team.Id, member);

        // Act
        var result = await _repository.GetByIdWithMembersAsync(team.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Members.Should().HaveCount(1);
        result.Members.First().UserId.Should().Be(_testUser.Id);
        result.Members.First().Role.Should().Be(TeamRole.Owner);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllTeams()
    {
        // Arrange
        var team1 = Team.Create("Team 1", _testUser.Id);
        var team2 = Team.Create("Team 2", _testUser.Id);
        await _repository.AddAsync(team1);
        await _repository.AddAsync(team2);

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        result.Should().HaveCountGreaterOrEqualTo(2);
        result.Should().Contain(t => t.Name == "Team 1");
        result.Should().Contain(t => t.Name == "Team 2");
    }

    [Fact]
    public async Task GetByUserIdAsync_ShouldReturnUserTeams()
    {
        // Arrange
        var team = Team.Create("User Team", _testUser.Id);
        await _repository.AddAsync(team);

        var member = TeamMember.Create(team.Id, _testUser.Id, TeamRole.Owner);
        await _repository.AddMemberAsync(team.Id, member);

        // Act
        var result = await _repository.GetByUserIdAsync(_testUser.Id);

        // Assert
        result.Should().HaveCountGreaterOrEqualTo(1);
        result.Should().Contain(t => t.Name == "User Team");
    }

    [Fact]
    public async Task UpdateAsync_ShouldModifyTeam()
    {
        // Arrange
        var team = Team.Create("Original Name", _testUser.Id);
        await _repository.AddAsync(team);

        // Act
        team.UpdateDetails("Updated Name", "Updated description");
        await _repository.UpdateAsync(team);

        // Assert
        var retrieved = await _repository.GetByIdAsync(team.Id);
        retrieved!.Name.Should().Be("Updated Name");
        retrieved.Description.Should().Be("Updated description");
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveTeam()
    {
        // Arrange
        var team = Team.Create("To Delete", _testUser.Id);
        await _repository.AddAsync(team);

        // Act
        await _repository.DeleteAsync(team.Id);

        // Assert
        var result = await _repository.GetByIdAsync(team.Id);
        result.Should().BeNull();
    }

    [Fact]
    public async Task ExistsAsync_WithExistingTeam_ShouldReturnTrue()
    {
        // Arrange
        var team = Team.Create("Exists Team", _testUser.Id);
        await _repository.AddAsync(team);

        // Act
        var result = await _repository.ExistsAsync(team.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WithNonExistentTeam_ShouldReturnFalse()
    {
        // Act
        var result = await _repository.ExistsAsync(Guid.NewGuid());

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task NameExistsAsync_WithExistingName_ShouldReturnTrue()
    {
        // Arrange
        var team = Team.Create("Unique Name", _testUser.Id);
        await _repository.AddAsync(team);

        // Act
        var result = await _repository.NameExistsAsync("Unique Name");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task NameExistsAsync_WithNonExistentName_ShouldReturnFalse()
    {
        // Act
        var result = await _repository.NameExistsAsync("Non Existent Name");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task AddMemberAsync_ShouldAddMemberToTeam()
    {
        // Arrange
        var team = Team.Create("Member Team", _testUser.Id);
        await _repository.AddAsync(team);

        var member = TeamMember.Create(team.Id, _testUser.Id, TeamRole.Admin);

        // Act
        await _repository.AddMemberAsync(team.Id, member);

        // Assert
        var retrieved = await _repository.GetByIdWithMembersAsync(team.Id);
        retrieved!.Members.Should().HaveCount(1);
        retrieved.Members.First().Role.Should().Be(TeamRole.Admin);
    }

    [Fact]
    public async Task RemoveMemberAsync_ShouldRemoveMemberFromTeam()
    {
        // Arrange
        var team = Team.Create("Remove Member Team", _testUser.Id);
        await _repository.AddAsync(team);

        var member = TeamMember.Create(team.Id, _testUser.Id, TeamRole.Member);
        await _repository.AddMemberAsync(team.Id, member);

        // Act
        await _repository.RemoveMemberAsync(team.Id, _testUser.Id);

        // Assert
        var retrieved = await _repository.GetByIdWithMembersAsync(team.Id);
        retrieved!.Members.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateMemberRoleAsync_ShouldChangeMemberRole()
    {
        // Arrange
        var team = Team.Create("Role Change Team", _testUser.Id);
        await _repository.AddAsync(team);

        var member = TeamMember.Create(team.Id, _testUser.Id, TeamRole.Member);
        await _repository.AddMemberAsync(team.Id, member);

        // Act
        await _repository.UpdateMemberRoleAsync(team.Id, _testUser.Id, TeamRole.Admin);

        // Assert
        var retrieved = await _repository.GetByIdWithMembersAsync(team.Id);
        retrieved!.Members.First().Role.Should().Be(TeamRole.Admin);
    }
}
