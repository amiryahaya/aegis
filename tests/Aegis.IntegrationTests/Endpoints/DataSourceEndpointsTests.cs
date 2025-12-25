using System.Net;
using System.Net.Http.Json;
using Aegis.IntegrationTests.Fixtures;
using FluentAssertions;

namespace Aegis.IntegrationTests.Endpoints;

[Collection("Integration")]
public class DataSourceEndpointsTests
{
    private readonly HttpClient _client;

    public DataSourceEndpointsTests(AegisApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateDataSource_WithValidData_ShouldReturnCreated()
    {
        // Arrange - First create a team
        var teamRequest = new
        {
            Name = $"Team-{Guid.NewGuid()}",
            OwnerId = await CreateTestUser(),
            Description = "Test team"
        };
        var teamResponse = await _client.PostAsJsonAsync("/api/teams", teamRequest);
        var team = await teamResponse.Content.ReadFromJsonAsync<TeamResponse>();

        var request = new
        {
            Name = "Test Data Source",
            TeamId = team!.Id,
            CreatedBy = teamRequest.OwnerId,
            Description = "Test description",
            Type = "Upload"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/datasources", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var dataSource = await response.Content.ReadFromJsonAsync<DataSourceResponse>();
        dataSource.Should().NotBeNull();
        dataSource!.Name.Should().Be("Test Data Source");
        dataSource.TeamId.Should().Be(team.Id);
        dataSource.Type.Should().Be("Upload");
        dataSource.Status.Should().Be("Active");
    }

    [Fact]
    public async Task CreateDataSource_WithNonExistentTeam_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new
        {
            Name = "Test Data Source",
            TeamId = Guid.NewGuid(),
            CreatedBy = Guid.NewGuid(),
            Type = "Upload"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/datasources", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateDataSource_WithDuplicateName_ShouldReturnConflict()
    {
        // Arrange
        var userId = await CreateTestUser();
        var teamRequest = new
        {
            Name = $"Team-{Guid.NewGuid()}",
            OwnerId = userId,
            Description = "Test team"
        };
        var teamResponse = await _client.PostAsJsonAsync("/api/teams", teamRequest);
        var team = await teamResponse.Content.ReadFromJsonAsync<TeamResponse>();

        var sourceName = $"Data Source {Guid.NewGuid()}";
        var firstRequest = new
        {
            Name = sourceName,
            TeamId = team!.Id,
            CreatedBy = userId,
            Type = "Upload"
        };

        await _client.PostAsJsonAsync("/api/datasources", firstRequest);

        // Act - Try to create with same name in same team
        var duplicateRequest = new
        {
            Name = sourceName,
            TeamId = team.Id,
            CreatedBy = userId,
            Type = "Upload"
        };
        var response = await _client.PostAsJsonAsync("/api/datasources", duplicateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetDataSource_WithValidId_ShouldReturnDataSource()
    {
        // Arrange
        var userId = await CreateTestUser();
        var teamRequest = new
        {
            Name = $"Team-{Guid.NewGuid()}",
            OwnerId = userId
        };
        var teamResponse = await _client.PostAsJsonAsync("/api/teams", teamRequest);
        var team = await teamResponse.Content.ReadFromJsonAsync<TeamResponse>();

        var createRequest = new
        {
            Name = "Test Source",
            TeamId = team!.Id,
            CreatedBy = userId,
            Type = "Upload"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/datasources", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<DataSourceResponse>();

        // Act
        var response = await _client.GetAsync($"/api/datasources/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dataSource = await response.Content.ReadFromJsonAsync<DataSourceResponse>();
        dataSource.Should().NotBeNull();
        dataSource!.Id.Should().Be(created.Id);
        dataSource.Name.Should().Be("Test Source");
    }

    [Fact]
    public async Task GetDataSource_WithNonExistentId_ShouldReturnNotFound()
    {
        // Act
        var response = await _client.GetAsync($"/api/datasources/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetDataSourcesByTeam_ShouldReturnOnlyTeamDataSources()
    {
        // Arrange
        var userId = await CreateTestUser();
        var teamRequest = new
        {
            Name = $"Team-{Guid.NewGuid()}",
            OwnerId = userId
        };
        var teamResponse = await _client.PostAsJsonAsync("/api/teams", teamRequest);
        var team = await teamResponse.Content.ReadFromJsonAsync<TeamResponse>();

        // Create two data sources for this team
        await _client.PostAsJsonAsync("/api/datasources", new
        {
            Name = $"Source 1-{Guid.NewGuid()}",
            TeamId = team!.Id,
            CreatedBy = userId,
            Type = "Upload"
        });

        await _client.PostAsJsonAsync("/api/datasources", new
        {
            Name = $"Source 2-{Guid.NewGuid()}",
            TeamId = team.Id,
            CreatedBy = userId,
            Type = "Api"
        });

        // Act
        var response = await _client.GetAsync($"/api/datasources?teamId={team.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dataSources = await response.Content.ReadFromJsonAsync<List<DataSourceResponse>>();
        dataSources.Should().NotBeNull();
        dataSources!.Should().HaveCountGreaterOrEqualTo(2);
        dataSources.Should().AllSatisfy(ds => ds.TeamId.Should().Be(team.Id));
    }

    [Fact]
    public async Task UpdateDataSource_ShouldModifyDataSource()
    {
        // Arrange
        var userId = await CreateTestUser();
        var teamRequest = new
        {
            Name = $"Team-{Guid.NewGuid()}",
            OwnerId = userId
        };
        var teamResponse = await _client.PostAsJsonAsync("/api/teams", teamRequest);
        var team = await teamResponse.Content.ReadFromJsonAsync<TeamResponse>();

        var createRequest = new
        {
            Name = "Original Name",
            TeamId = team!.Id,
            CreatedBy = userId,
            Type = "Upload"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/datasources", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<DataSourceResponse>();

        var updateRequest = new
        {
            Name = "Updated Name",
            Description = "Updated description"
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/datasources/{created!.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await response.Content.ReadFromJsonAsync<DataSourceResponse>();
        updated!.Name.Should().Be("Updated Name");
        updated.Description.Should().Be("Updated description");
    }

    [Fact]
    public async Task DeleteDataSource_ShouldRemoveDataSource()
    {
        // Arrange
        var userId = await CreateTestUser();
        var teamRequest = new
        {
            Name = $"Team-{Guid.NewGuid()}",
            OwnerId = userId
        };
        var teamResponse = await _client.PostAsJsonAsync("/api/teams", teamRequest);
        var team = await teamResponse.Content.ReadFromJsonAsync<TeamResponse>();

        var createRequest = new
        {
            Name = $"To Delete-{Guid.NewGuid()}",
            TeamId = team!.Id,
            CreatedBy = userId,
            Type = "Upload"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/datasources", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<DataSourceResponse>();

        // Act
        var response = await _client.DeleteAsync($"/api/datasources/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it's deleted
        var getResponse = await _client.GetAsync($"/api/datasources/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<Guid> CreateTestUser()
    {
        var email = $"{Guid.NewGuid()}@test.com";
        var request = new
        {
            Email = email,
            Name = "Test User"
        };
        var response = await _client.PostAsJsonAsync("/api/users", request);
        var user = await response.Content.ReadFromJsonAsync<UserResponse>();
        return user!.Id;
    }
}

public record DataSourceResponse(
    Guid Id,
    string Name,
    string? Description,
    Guid TeamId,
    Guid? WorkspaceId,
    string Type,
    string Status,
    Guid CreatedBy,
    int DocumentCount,
    long TotalSizeBytes);

public record TeamResponse(Guid Id, string Name, string? Description, Guid CreatedBy, bool IsActive);
public record UserResponse(Guid Id, string Email, string Name, string Role, bool IsActive);
