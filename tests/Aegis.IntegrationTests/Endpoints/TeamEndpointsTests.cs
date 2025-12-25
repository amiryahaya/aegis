using System.Net;
using System.Net.Http.Json;
using Aegis.IntegrationTests.Fixtures;

namespace Aegis.IntegrationTests.Endpoints;

[Collection("Integration")]
public class TeamEndpointsTests
{
    private readonly HttpClient _client;

    public TeamEndpointsTests(AegisApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static string UniqueName() => $"Team-{Guid.NewGuid():N}"[..30];

    private async Task<UserResponse> CreateTestUserAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/users", new
        {
            Email = $"{Guid.NewGuid()}@test.com",
            Name = "Test User"
        });
        return (await response.Content.ReadFromJsonAsync<UserResponse>())!;
    }

    [Fact]
    public async Task CreateTeam_ShouldReturnCreated()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var request = new
        {
            Name = UniqueName(),
            Description = "A test team",
            OwnerId = user.Id
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/teams", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var team = await response.Content.ReadFromJsonAsync<TeamResponse>();
        team.Should().NotBeNull();
        team!.Name.Should().Be(request.Name);
        team.Description.Should().Be("A test team");
        team.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateTeam_WithDuplicateName_ShouldReturnConflict()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var name = UniqueName();
        var request = new { Name = name, OwnerId = user.Id };
        await _client.PostAsJsonAsync("/api/teams", request);

        // Act
        var response = await _client.PostAsJsonAsync("/api/teams", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetTeam_ShouldReturnTeam()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var createResponse = await _client.PostAsJsonAsync("/api/teams", new
        {
            Name = UniqueName(),
            OwnerId = user.Id
        });
        var createdTeam = await createResponse.Content.ReadFromJsonAsync<TeamResponse>();

        // Act
        var response = await _client.GetAsync($"/api/teams/{createdTeam!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var team = await response.Content.ReadFromJsonAsync<TeamResponse>();
        team.Should().NotBeNull();
        team!.Id.Should().Be(createdTeam.Id);
    }

    [Fact]
    public async Task GetTeam_WithNonExistentId_ShouldReturnNotFound()
    {
        // Act
        var response = await _client.GetAsync($"/api/teams/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAllTeams_ShouldReturnList()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        await _client.PostAsJsonAsync("/api/teams", new { Name = UniqueName(), OwnerId = user.Id });
        await _client.PostAsJsonAsync("/api/teams", new { Name = UniqueName(), OwnerId = user.Id });

        // Act
        var response = await _client.GetAsync("/api/teams");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var teams = await response.Content.ReadFromJsonAsync<List<TeamResponse>>();
        teams.Should().NotBeNull();
        teams!.Count.Should().BeGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task UpdateTeam_ShouldModifyTeam()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var createResponse = await _client.PostAsJsonAsync("/api/teams", new
        {
            Name = UniqueName(),
            OwnerId = user.Id
        });
        var createdTeam = await createResponse.Content.ReadFromJsonAsync<TeamResponse>();

        // Act
        var updateRequest = new { Name = UniqueName(), Description = "Updated description" };
        var response = await _client.PutAsJsonAsync($"/api/teams/{createdTeam!.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var team = await response.Content.ReadFromJsonAsync<TeamResponse>();
        team!.Description.Should().Be("Updated description");
    }

    [Fact]
    public async Task UpdateTeam_WithNonExistentId_ShouldReturnNotFound()
    {
        // Act
        var response = await _client.PutAsJsonAsync($"/api/teams/{Guid.NewGuid()}", new { Name = "Test" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteTeam_ShouldRemoveTeam()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var createResponse = await _client.PostAsJsonAsync("/api/teams", new
        {
            Name = UniqueName(),
            OwnerId = user.Id
        });
        var createdTeam = await createResponse.Content.ReadFromJsonAsync<TeamResponse>();

        // Act
        var response = await _client.DeleteAsync($"/api/teams/{createdTeam!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await _client.GetAsync($"/api/teams/{createdTeam.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteTeam_WithNonExistentId_ShouldReturnNotFound()
    {
        // Act
        var response = await _client.DeleteAsync($"/api/teams/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddMember_ShouldAddMemberToTeam()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var createResponse = await _client.PostAsJsonAsync("/api/teams", new
        {
            Name = UniqueName(),
            OwnerId = user.Id
        });
        var team = await createResponse.Content.ReadFromJsonAsync<TeamResponse>();

        // Act
        var response = await _client.PostAsJsonAsync($"/api/teams/{team!.Id}/members", new
        {
            UserId = user.Id,
            Role = "Admin"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task RemoveMember_ShouldRemoveMemberFromTeam()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var createResponse = await _client.PostAsJsonAsync("/api/teams", new
        {
            Name = UniqueName(),
            OwnerId = user.Id
        });
        var team = await createResponse.Content.ReadFromJsonAsync<TeamResponse>();
        await _client.PostAsJsonAsync($"/api/teams/{team!.Id}/members", new
        {
            UserId = user.Id,
            Role = "Member"
        });

        // Act
        var response = await _client.DeleteAsync($"/api/teams/{team.Id}/members/{user.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private record UserResponse(Guid Id, string Email, string Name, string Role, bool IsActive);
    private record TeamResponse(Guid Id, string Name, string? Description, Guid CreatedBy, bool IsActive);
}
