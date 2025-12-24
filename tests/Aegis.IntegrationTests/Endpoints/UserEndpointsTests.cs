using System.Net;
using System.Net.Http.Json;
using Aegis.IntegrationTests.Fixtures;

namespace Aegis.IntegrationTests.Endpoints;

[Collection("Integration")]
public class UserEndpointsTests
{
    private readonly HttpClient _client;

    public UserEndpointsTests(AegisApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static string UniqueEmail() => $"{Guid.NewGuid()}@test.com";

    [Fact]
    public async Task CreateUser_ShouldReturnCreated()
    {
        // Arrange
        var email = UniqueEmail();
        var request = new
        {
            Email = email,
            Name = "New User"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/users", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var user = await response.Content.ReadFromJsonAsync<UserResponse>();
        user.Should().NotBeNull();
        user!.Email.Should().Be(email);
        user.Name.Should().Be("New User");
        user.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateUser_WithInvalidEmail_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new
        {
            Email = "invalid-email",
            Name = "Test User"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/users", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateUser_WithDuplicateEmail_ShouldReturnConflict()
    {
        // Arrange
        var email = UniqueEmail();
        var request = new
        {
            Email = email,
            Name = "First User"
        };
        await _client.PostAsJsonAsync("/api/users", request);

        // Act - Try to create another user with same email
        var response = await _client.PostAsJsonAsync("/api/users", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetUser_ShouldReturnUser()
    {
        // Arrange - Create a user first
        var email = UniqueEmail();
        var createRequest = new
        {
            Email = email,
            Name = "Get User"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/users", createRequest);
        var createdUser = await createResponse.Content.ReadFromJsonAsync<UserResponse>();

        // Act
        var response = await _client.GetAsync($"/api/users/{createdUser!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var user = await response.Content.ReadFromJsonAsync<UserResponse>();
        user.Should().NotBeNull();
        user!.Id.Should().Be(createdUser.Id);
        user.Email.Should().Be(email);
    }

    [Fact]
    public async Task GetUser_WithNonExistentId_ShouldReturnNotFound()
    {
        // Act
        var response = await _client.GetAsync($"/api/users/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAllUsers_ShouldReturnList()
    {
        // Arrange - Create some users
        await _client.PostAsJsonAsync("/api/users", new { Email = UniqueEmail(), Name = "User 1" });
        await _client.PostAsJsonAsync("/api/users", new { Email = UniqueEmail(), Name = "User 2" });

        // Act
        var response = await _client.GetAsync("/api/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var users = await response.Content.ReadFromJsonAsync<List<UserResponse>>();
        users.Should().NotBeNull();
        users!.Count.Should().BeGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task UpdateUser_ShouldModifyUser()
    {
        // Arrange - Create a user first
        var createRequest = new { Email = UniqueEmail(), Name = "Original Name" };
        var createResponse = await _client.PostAsJsonAsync("/api/users", createRequest);
        var createdUser = await createResponse.Content.ReadFromJsonAsync<UserResponse>();

        // Act
        var updateRequest = new { Name = "Updated Name" };
        var response = await _client.PutAsJsonAsync($"/api/users/{createdUser!.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var user = await response.Content.ReadFromJsonAsync<UserResponse>();
        user!.Name.Should().Be("Updated Name");
    }

    [Fact]
    public async Task UpdateUser_WithNonExistentId_ShouldReturnNotFound()
    {
        // Act
        var response = await _client.PutAsJsonAsync($"/api/users/{Guid.NewGuid()}", new { Name = "Test" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteUser_ShouldRemoveUser()
    {
        // Arrange - Create a user first
        var createRequest = new { Email = UniqueEmail(), Name = "To Delete" };
        var createResponse = await _client.PostAsJsonAsync("/api/users", createRequest);
        var createdUser = await createResponse.Content.ReadFromJsonAsync<UserResponse>();

        // Act
        var response = await _client.DeleteAsync($"/api/users/{createdUser!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify user is deleted
        var getResponse = await _client.GetAsync($"/api/users/{createdUser.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteUser_WithNonExistentId_ShouldReturnNotFound()
    {
        // Act
        var response = await _client.DeleteAsync($"/api/users/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private record UserResponse(Guid Id, string Email, string Name, string Role, bool IsActive);
}
