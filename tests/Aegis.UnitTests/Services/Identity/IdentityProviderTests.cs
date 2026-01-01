using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.Identity;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Aegis.UnitTests.Services.Identity;

public class IdentityProviderTests
{
    private readonly InMemoryIdentityProvider _provider;
    private readonly Mock<ILogger<InMemoryIdentityProvider>> _loggerMock;

    public IdentityProviderTests()
    {
        _loggerMock = new Mock<ILogger<InMemoryIdentityProvider>>();
        _provider = new InMemoryIdentityProvider(_loggerMock.Object);
    }

    #region GetAuthorizationUrlAsync Tests

    [Fact]
    public async Task GetAuthorizationUrlAsync_WithDefaultProvider_ReturnsUrl()
    {
        // Arrange
        var request = new AuthorizationRequest();

        // Act
        var result = await _provider.GetAuthorizationUrlAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Url.Should().NotBeNullOrEmpty();
        result.Value.State.Should().NotBeNullOrEmpty();
        result.Value.Nonce.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetAuthorizationUrlAsync_WithPkce_ReturnsCodeVerifier()
    {
        // Arrange
        var request = new AuthorizationRequest();

        // Act
        var result = await _provider.GetAuthorizationUrlAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CodeVerifier.Should().NotBeNullOrEmpty();
        result.Value.Url.Should().Contain("code_challenge");
        result.Value.Url.Should().Contain("code_challenge_method=S256");
    }

    [Fact]
    public async Task GetAuthorizationUrlAsync_WithCustomState_UsesProvidedState()
    {
        // Arrange
        var customState = "my-custom-state-123";
        var request = new AuthorizationRequest { State = customState };

        // Act
        var result = await _provider.GetAuthorizationUrlAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.State.Should().Be(customState);
        result.Value.Url.Should().Contain($"state={Uri.EscapeDataString(customState)}");
    }

    [Fact]
    public async Task GetAuthorizationUrlAsync_WithLoginHint_IncludesInUrl()
    {
        // Arrange
        var request = new AuthorizationRequest { LoginHint = "user@example.com" };

        // Act
        var result = await _provider.GetAuthorizationUrlAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Url.Should().Contain("login_hint=user%40example.com");
    }

    [Fact]
    public async Task GetAuthorizationUrlAsync_WithPrompt_IncludesInUrl()
    {
        // Arrange
        var request = new AuthorizationRequest { Prompt = "consent" };

        // Act
        var result = await _provider.GetAuthorizationUrlAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Url.Should().Contain("prompt=consent");
    }

    [Fact]
    public async Task GetAuthorizationUrlAsync_WithInvalidProvider_ReturnsFailure()
    {
        // Arrange
        var request = new AuthorizationRequest { ProviderId = "non-existent" };

        // Act
        var result = await _provider.GetAuthorizationUrlAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
    }

    [Fact]
    public async Task GetAuthorizationUrlAsync_SetsExpirationTime()
    {
        // Arrange
        var request = new AuthorizationRequest();

        // Act
        var result = await _provider.GetAuthorizationUrlAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);
        result.Value.ExpiresAt.Should().BeBefore(DateTimeOffset.UtcNow.AddMinutes(15));
    }

    #endregion

    #region ExchangeCodeAsync Tests

    [Fact]
    public async Task ExchangeCodeAsync_WithValidCode_ReturnsTokens()
    {
        // Arrange
        var authResult = await _provider.GetAuthorizationUrlAsync(new AuthorizationRequest());
        var request = new CodeExchangeRequest
        {
            Code = "code_test@example.com",
            State = authResult.Value.State,
            CodeVerifier = authResult.Value.CodeVerifier
        };

        // Act
        var result = await _provider.ExchangeCodeAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().NotBeNullOrEmpty();
        result.Value.RefreshToken.Should().NotBeNullOrEmpty();
        result.Value.TokenType.Should().Be("Bearer");
        result.Value.ExpiresIn.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExchangeCodeAsync_ReturnsUserInfo()
    {
        // Arrange
        var authResult = await _provider.GetAuthorizationUrlAsync(new AuthorizationRequest());
        var request = new CodeExchangeRequest
        {
            Code = "code_user@test.com",
            State = authResult.Value.State,
            CodeVerifier = authResult.Value.CodeVerifier
        };

        // Act
        var result = await _provider.ExchangeCodeAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserInfo.Should().NotBeNull();
        result.Value.UserInfo!.Email.Should().Be("user@test.com");
    }

    [Fact]
    public async Task ExchangeCodeAsync_WithInvalidState_ReturnsFailure()
    {
        // Arrange
        var request = new CodeExchangeRequest
        {
            Code = "valid_code",
            State = "invalid-state",
            CodeVerifier = "some-verifier"
        };

        // Act
        var result = await _provider.ExchangeCodeAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("InvalidState");
    }

    [Fact]
    public async Task ExchangeCodeAsync_WithoutCodeVerifier_ReturnsFailure()
    {
        // Arrange
        var authResult = await _provider.GetAuthorizationUrlAsync(new AuthorizationRequest());
        var request = new CodeExchangeRequest
        {
            Code = "valid_code",
            State = authResult.Value.State
            // CodeVerifier is missing
        };

        // Act
        var result = await _provider.ExchangeCodeAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("PkceRequired");
    }

    [Fact]
    public async Task ExchangeCodeAsync_StateCanOnlyBeUsedOnce()
    {
        // Arrange
        var authResult = await _provider.GetAuthorizationUrlAsync(new AuthorizationRequest());
        var request = new CodeExchangeRequest
        {
            Code = "code_test@example.com",
            State = authResult.Value.State,
            CodeVerifier = authResult.Value.CodeVerifier
        };

        // First exchange should succeed
        var result1 = await _provider.ExchangeCodeAsync(request);
        result1.IsSuccess.Should().BeTrue();

        // Act - Second exchange with same state should fail
        var result2 = await _provider.ExchangeCodeAsync(request);

        // Assert
        result2.IsFailure.Should().BeTrue();
        result2.Error.Code.Should().Contain("InvalidState");
    }

    #endregion

    #region RefreshTokenAsync Tests

    [Fact]
    public async Task RefreshTokenAsync_WithValidToken_ReturnsNewAccessToken()
    {
        // Arrange
        var authResult = await _provider.GetAuthorizationUrlAsync(new AuthorizationRequest());
        var tokenResult = await _provider.ExchangeCodeAsync(new CodeExchangeRequest
        {
            Code = "code_test@example.com",
            State = authResult.Value.State,
            CodeVerifier = authResult.Value.CodeVerifier
        });

        // Act
        var result = await _provider.RefreshTokenAsync(tokenResult.Value.RefreshToken!);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().NotBeNullOrEmpty();
        result.Value.AccessToken.Should().NotBe(tokenResult.Value.AccessToken);
        result.Value.RefreshToken.Should().Be(tokenResult.Value.RefreshToken);
    }

    [Fact]
    public async Task RefreshTokenAsync_WithInvalidToken_ReturnsFailure()
    {
        // Act
        var result = await _provider.RefreshTokenAsync("invalid-refresh-token");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("InvalidToken");
    }

    [Fact]
    public async Task RefreshTokenAsync_WithRevokedToken_ReturnsFailure()
    {
        // Arrange
        var authResult = await _provider.GetAuthorizationUrlAsync(new AuthorizationRequest());
        var tokenResult = await _provider.ExchangeCodeAsync(new CodeExchangeRequest
        {
            Code = "code_test@example.com",
            State = authResult.Value.State,
            CodeVerifier = authResult.Value.CodeVerifier
        });

        // Revoke the refresh token
        await _provider.RevokeTokenAsync(tokenResult.Value.RefreshToken!, TokenType.RefreshToken);

        // Act
        var result = await _provider.RefreshTokenAsync(tokenResult.Value.RefreshToken!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("TokenRevoked");
    }

    #endregion

    #region ValidateTokenAsync Tests

    [Fact]
    public async Task ValidateTokenAsync_WithValidToken_ReturnsUserInfo()
    {
        // Arrange
        var authResult = await _provider.GetAuthorizationUrlAsync(new AuthorizationRequest());
        var tokenResult = await _provider.ExchangeCodeAsync(new CodeExchangeRequest
        {
            Code = "code_admin@aegis.local",
            State = authResult.Value.State,
            CodeVerifier = authResult.Value.CodeVerifier
        });

        // Act
        var result = await _provider.ValidateTokenAsync(tokenResult.Value.AccessToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be("admin@aegis.local");
    }

    [Fact]
    public async Task ValidateTokenAsync_WithInvalidToken_ReturnsFailure()
    {
        // Act
        var result = await _provider.ValidateTokenAsync("invalid-access-token");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("InvalidToken");
    }

    [Fact]
    public async Task ValidateTokenAsync_WithRevokedToken_ReturnsFailure()
    {
        // Arrange
        var authResult = await _provider.GetAuthorizationUrlAsync(new AuthorizationRequest());
        var tokenResult = await _provider.ExchangeCodeAsync(new CodeExchangeRequest
        {
            Code = "code_test@example.com",
            State = authResult.Value.State,
            CodeVerifier = authResult.Value.CodeVerifier
        });

        // Revoke the token
        await _provider.RevokeTokenAsync(tokenResult.Value.AccessToken);

        // Act
        var result = await _provider.ValidateTokenAsync(tokenResult.Value.AccessToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("TokenRevoked");
    }

    #endregion

    #region GetUserInfoAsync Tests

    [Fact]
    public async Task GetUserInfoAsync_WithValidToken_ReturnsUserInfo()
    {
        // Arrange
        var authResult = await _provider.GetAuthorizationUrlAsync(new AuthorizationRequest());
        var tokenResult = await _provider.ExchangeCodeAsync(new CodeExchangeRequest
        {
            Code = "code_user@aegis.local",
            State = authResult.Value.State,
            CodeVerifier = authResult.Value.CodeVerifier
        });

        // Act
        var result = await _provider.GetUserInfoAsync(tokenResult.Value.AccessToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be("user@aegis.local");
        result.Value.Roles.Should().Contain("User");
    }

    #endregion

    #region RevokeTokenAsync Tests

    [Fact]
    public async Task RevokeTokenAsync_WithValidToken_ReturnsTrue()
    {
        // Arrange
        var authResult = await _provider.GetAuthorizationUrlAsync(new AuthorizationRequest());
        var tokenResult = await _provider.ExchangeCodeAsync(new CodeExchangeRequest
        {
            Code = "code_test@example.com",
            State = authResult.Value.State,
            CodeVerifier = authResult.Value.CodeVerifier
        });

        // Act
        var result = await _provider.RevokeTokenAsync(tokenResult.Value.AccessToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task RevokeTokenAsync_WithInvalidToken_ReturnsFalse()
    {
        // Act
        var result = await _provider.RevokeTokenAsync("non-existent-token");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
    }

    #endregion

    #region LogoutAsync Tests

    [Fact]
    public async Task LogoutAsync_ReturnsLogoutUrl()
    {
        // Arrange
        var request = new LogoutRequest();

        // Act
        var result = await _provider.LogoutAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.LogoutUrl.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task LogoutAsync_WithState_IncludesStateInUrl()
    {
        // Arrange
        var request = new LogoutRequest { State = "logout-state-123" };

        // Act
        var result = await _provider.LogoutAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.State.Should().Be("logout-state-123");
        result.Value.LogoutUrl.Should().Contain("state=logout-state-123");
    }

    #endregion

    #region GetDiscoveryDocumentAsync Tests

    [Fact]
    public async Task GetDiscoveryDocumentAsync_ReturnsDiscoveryDocument()
    {
        // Act
        var result = await _provider.GetDiscoveryDocumentAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Issuer.Should().NotBeNullOrEmpty();
        result.Value.AuthorizationEndpoint.Should().NotBeNullOrEmpty();
        result.Value.TokenEndpoint.Should().NotBeNullOrEmpty();
        result.Value.UserInfoEndpoint.Should().NotBeNullOrEmpty();
        result.Value.JwksUri.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetDiscoveryDocumentAsync_IncludesSupportedFeatures()
    {
        // Act
        var result = await _provider.GetDiscoveryDocumentAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ScopesSupported.Should().NotBeEmpty();
        result.Value.ResponseTypesSupported.Should().Contain("code");
        result.Value.GrantTypesSupported.Should().Contain("authorization_code");
        result.Value.CodeChallengeMethodsSupported.Should().BeTrue();
    }

    #endregion

    #region GetProvidersAsync Tests

    [Fact]
    public async Task GetProvidersAsync_ReturnsProviderList()
    {
        // Act
        var result = await _provider.GetProvidersAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        result.Value.Should().Contain(p => p.ProviderId == "local");
    }

    [Fact]
    public async Task GetProvidersAsync_IncludesProviderDetails()
    {
        // Act
        var result = await _provider.GetProvidersAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        var provider = result.Value.First();
        provider.Name.Should().NotBeNullOrEmpty();
        provider.Type.Should().BeDefined();
        provider.IsEnabled.Should().BeTrue();
    }

    #endregion

    #region Custom Provider Tests

    [Fact]
    public async Task WithCustomProviders_UsesCustomConfiguration()
    {
        // Arrange
        var customProviders = new List<IdentityProviderConfig>
        {
            new IdentityProviderConfig
            {
                ProviderId = "azure-ad",
                Name = "Azure Active Directory",
                Type = IdentityProviderType.AzureAD,
                Authority = "https://login.microsoftonline.com/tenant-id",
                ClientId = "custom-client-id",
                RedirectUri = "https://myapp.com/callback",
                Scopes = new List<string> { "openid", "profile", "email", "User.Read" }
            }
        };

        var customProvider = new InMemoryIdentityProvider(_loggerMock.Object, customProviders);

        // Act
        var result = await customProvider.GetAuthorizationUrlAsync(new AuthorizationRequest
        {
            ProviderId = "azure-ad"
        });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Url.Should().Contain("login.microsoftonline.com");
        result.Value.Url.Should().Contain("custom-client-id");
    }

    #endregion

    #region Seeded Users Tests

    [Fact]
    public async Task SeededUsers_AreAvailable()
    {
        // Arrange
        var authResult = await _provider.GetAuthorizationUrlAsync(new AuthorizationRequest());

        // Test with seeded admin user
        var tokenResult = await _provider.ExchangeCodeAsync(new CodeExchangeRequest
        {
            Code = "code_admin@aegis.local",
            State = authResult.Value.State,
            CodeVerifier = authResult.Value.CodeVerifier
        });

        // Act
        var userInfo = await _provider.GetUserInfoAsync(tokenResult.Value.AccessToken);

        // Assert
        userInfo.IsSuccess.Should().BeTrue();
        userInfo.Value.Email.Should().Be("admin@aegis.local");
        userInfo.Value.Roles.Should().Contain("Admin");
        userInfo.Value.Name.Should().Be("Admin User");
    }

    #endregion
}
