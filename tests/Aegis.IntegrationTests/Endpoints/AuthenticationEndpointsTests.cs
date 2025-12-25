using System.Net;
using System.Net.Http.Json;
using Aegis.IntegrationTests.Fixtures;
using FluentAssertions;

namespace Aegis.IntegrationTests.Endpoints;

[Collection("Integration")]
public class AuthenticationEndpointsTests
{
    private readonly HttpClient _client;

    public AuthenticationEndpointsTests(AegisApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static string UniqueEmail() => $"{Guid.NewGuid()}@test.com";

    [Fact]
    public async Task Register_WithValidData_ShouldReturnCreated()
    {
        // Arrange
        var email = UniqueEmail();
        var request = new
        {
            Email = email,
            Name = "Test User",
            Password = "Password123"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var registerResponse = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        registerResponse.Should().NotBeNull();
        registerResponse!.Email.Should().Be(email);
        registerResponse.Name.Should().Be("Test User");
        registerResponse.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ShouldReturnConflict()
    {
        // Arrange
        var email = UniqueEmail();
        var request = new
        {
            Email = email,
            Name = "Test User",
            Password = "Password123"
        };

        // Act - Register first time
        await _client.PostAsJsonAsync("/api/auth/register", request);

        // Act - Try to register again with same email
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_WithInvalidEmail_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new
        {
            Email = "invalid-email",
            Name = "Test User",
            Password = "Password123"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithWeakPassword_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new
        {
            Email = UniqueEmail(),
            Name = "Test User",
            Password = "weak"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ShouldReturnToken()
    {
        // Arrange - Register a user first
        var email = UniqueEmail();
        var password = "Password123";
        var registerRequest = new
        {
            Email = email,
            Name = "Test User",
            Password = password
        };
        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        // Act - Login
        var loginRequest = new
        {
            Email = email,
            Password = password
        };
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        loginResponse.Should().NotBeNull();
        loginResponse!.Token.Should().NotBeNullOrEmpty();
        loginResponse.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task Login_WithInvalidEmail_ShouldReturnUnauthorized()
    {
        // Arrange
        var loginRequest = new
        {
            Email = "nonexistent@example.com",
            Password = "Password123"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ShouldReturnUnauthorized()
    {
        // Arrange - Register a user first
        var email = UniqueEmail();
        var registerRequest = new
        {
            Email = email,
            Name = "Test User",
            Password = "Password123"
        };
        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        // Act - Login with wrong password
        var loginRequest = new
        {
            Email = email,
            Password = "WrongPassword123"
        };
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_AfterMultipleFailedAttempts_ShouldLockAccount()
    {
        // Arrange - Register a user
        var email = UniqueEmail();
        var correctPassword = "Password123";
        var registerRequest = new
        {
            Email = email,
            Name = "Test User",
            Password = correctPassword
        };
        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        // Act - Attempt login with wrong password multiple times
        for (int i = 0; i < 5; i++)
        {
            var loginRequest = new
            {
                Email = email,
                Password = "WrongPassword123"
            };
            await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        }

        // Try one more time with correct password
        var finalLoginRequest = new
        {
            Email = email,
            Password = correctPassword
        };
        var response = await _client.PostAsJsonAsync("/api/auth/login", finalLoginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

public record RegisterResponse(Guid Id, string Email, string Name);
public record LoginResponse(string Token, DateTime ExpiresAt);
