using System.Net;
using System.Net.Http.Json;
using Aegis.IntegrationTests.Fixtures;

namespace Aegis.IntegrationTests.Endpoints;

[Collection("Integration")]
public class WorkspaceEndpointsTests
{
    private readonly HttpClient _client;

    public WorkspaceEndpointsTests(AegisApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static string UniqueName() => $"Workspace-{Guid.NewGuid():N}"[..30];

    private async Task<UserResponse> CreateTestUserAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/users", new
        {
            Email = $"{Guid.NewGuid()}@test.com",
            Name = "Test User"
        });
        return (await response.Content.ReadFromJsonAsync<UserResponse>())!;
    }

    private async Task<TeamResponse> CreateTestTeamAsync(Guid ownerId)
    {
        var response = await _client.PostAsJsonAsync("/api/teams", new
        {
            Name = $"Team-{Guid.NewGuid():N}"[..20],
            OwnerId = ownerId
        });
        return (await response.Content.ReadFromJsonAsync<TeamResponse>())!;
    }

    [Fact]
    public async Task CreateWorkspace_ShouldReturnCreated()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var request = new
        {
            Name = UniqueName(),
            Description = "A test workspace",
            CreatedBy = user.Id
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/workspaces", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var workspace = await response.Content.ReadFromJsonAsync<WorkspaceResponse>();
        workspace.Should().NotBeNull();
        workspace!.Name.Should().Be(request.Name);
        workspace.Description.Should().Be("A test workspace");
        workspace.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateWorkspace_WithTeam_ShouldReturnCreated()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var team = await CreateTestTeamAsync(user.Id);
        var request = new
        {
            Name = UniqueName(),
            CreatedBy = user.Id,
            TeamId = team.Id
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/workspaces", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var workspace = await response.Content.ReadFromJsonAsync<WorkspaceResponse>();
        workspace!.TeamId.Should().Be(team.Id);
    }

    [Fact]
    public async Task GetWorkspace_ShouldReturnWorkspace()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var createResponse = await _client.PostAsJsonAsync("/api/workspaces", new
        {
            Name = UniqueName(),
            CreatedBy = user.Id
        });
        var createdWorkspace = await createResponse.Content.ReadFromJsonAsync<WorkspaceResponse>();

        // Act
        var response = await _client.GetAsync($"/api/workspaces/{createdWorkspace!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var workspace = await response.Content.ReadFromJsonAsync<WorkspaceResponse>();
        workspace.Should().NotBeNull();
        workspace!.Id.Should().Be(createdWorkspace.Id);
    }

    [Fact]
    public async Task GetWorkspace_WithNonExistentId_ShouldReturnNotFound()
    {
        // Act
        var response = await _client.GetAsync($"/api/workspaces/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAllWorkspaces_ShouldReturnList()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        await _client.PostAsJsonAsync("/api/workspaces", new { Name = UniqueName(), CreatedBy = user.Id });
        await _client.PostAsJsonAsync("/api/workspaces", new { Name = UniqueName(), CreatedBy = user.Id });

        // Act
        var response = await _client.GetAsync("/api/workspaces");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var workspaces = await response.Content.ReadFromJsonAsync<List<WorkspaceResponse>>();
        workspaces.Should().NotBeNull();
        workspaces!.Count.Should().BeGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task UpdateWorkspace_ShouldModifyWorkspace()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var createResponse = await _client.PostAsJsonAsync("/api/workspaces", new
        {
            Name = UniqueName(),
            CreatedBy = user.Id
        });
        var createdWorkspace = await createResponse.Content.ReadFromJsonAsync<WorkspaceResponse>();

        // Act
        var updateRequest = new { Name = UniqueName(), Description = "Updated description" };
        var response = await _client.PutAsJsonAsync($"/api/workspaces/{createdWorkspace!.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var workspace = await response.Content.ReadFromJsonAsync<WorkspaceResponse>();
        workspace!.Description.Should().Be("Updated description");
    }

    [Fact]
    public async Task UpdateWorkspace_WithNonExistentId_ShouldReturnNotFound()
    {
        // Act
        var response = await _client.PutAsJsonAsync($"/api/workspaces/{Guid.NewGuid()}", new { Name = "Test" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteWorkspace_ShouldRemoveWorkspace()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var createResponse = await _client.PostAsJsonAsync("/api/workspaces", new
        {
            Name = UniqueName(),
            CreatedBy = user.Id
        });
        var createdWorkspace = await createResponse.Content.ReadFromJsonAsync<WorkspaceResponse>();

        // Act
        var response = await _client.DeleteAsync($"/api/workspaces/{createdWorkspace!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await _client.GetAsync($"/api/workspaces/{createdWorkspace.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteWorkspace_WithNonExistentId_ShouldReturnNotFound()
    {
        // Act
        var response = await _client.DeleteAsync($"/api/workspaces/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ArchiveWorkspace_ShouldChangeStatus()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var createResponse = await _client.PostAsJsonAsync("/api/workspaces", new
        {
            Name = UniqueName(),
            CreatedBy = user.Id
        });
        var createdWorkspace = await createResponse.Content.ReadFromJsonAsync<WorkspaceResponse>();

        // Act
        var response = await _client.PostAsync($"/api/workspaces/{createdWorkspace!.Id}/archive", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var workspace = await response.Content.ReadFromJsonAsync<WorkspaceResponse>();
        workspace!.Status.Should().Be("Archived");
    }

    private record UserResponse(Guid Id, string Email, string Name, string Role, bool IsActive);
    private record TeamResponse(Guid Id, string Name, string? Description, Guid CreatedBy, bool IsActive);
    private record WorkspaceResponse(Guid Id, string Name, string? Description, Guid? TeamId, Guid CreatedBy, string Status);
}
