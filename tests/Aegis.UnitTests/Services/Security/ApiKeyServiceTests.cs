using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.Security;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Aegis.UnitTests.Services.Security;

public class ApiKeyServiceTests
{
    private readonly ILogger<InMemoryApiKeyService> _logger;
    private readonly InMemoryApiKeyService _sut;

    public ApiKeyServiceTests()
    {
        _logger = Substitute.For<ILogger<InMemoryApiKeyService>>();
        _sut = new InMemoryApiKeyService(_logger);
    }

    #region Generate

    [Fact]
    public async Task GenerateAsync_CreatesValidApiKey()
    {
        // Arrange
        var request = new ApiKeyRequest
        {
            Name = "Test API Key",
            UserId = Guid.NewGuid(),
            Scopes = new List<string> { "read", "write" }
        };

        // Act
        var result = await _sut.GenerateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ApiKey.Should().NotBeNullOrEmpty();
        result.Value.Prefix.Should().Be("aegis_");
        result.Value.Name.Should().Be("Test API Key");
        result.Value.Scopes.Should().Contain("read");
        result.Value.Scopes.Should().Contain("write");
    }

    [Fact]
    public async Task GenerateAsync_KeyHasCorrectFormat()
    {
        // Arrange
        var request = new ApiKeyRequest
        {
            Name = "Format Test",
            UserId = Guid.NewGuid()
        };

        // Act
        var result = await _sut.GenerateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ApiKey.Should().StartWith("aegis_");
        result.Value.ApiKey.Length.Should().BeGreaterThan(20);
        result.Value.LastFour.Length.Should().Be(4);
        result.Value.ApiKey.Should().EndWith(result.Value.LastFour);
    }

    [Fact]
    public async Task GenerateAsync_WithExpiration_SetsExpiresAt()
    {
        // Arrange
        var expiresAt = DateTime.UtcNow.AddDays(30);
        var request = new ApiKeyRequest
        {
            Name = "Expiring Key",
            UserId = Guid.NewGuid(),
            ExpiresAt = expiresAt
        };

        // Act
        var result = await _sut.GenerateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ExpiresAt.Should().BeCloseTo(expiresAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task GenerateAsync_GeneratesUniqueKeys()
    {
        // Arrange
        var request = new ApiKeyRequest
        {
            Name = "Unique Key",
            UserId = Guid.NewGuid()
        };

        // Act
        var result1 = await _sut.GenerateAsync(request);
        var result2 = await _sut.GenerateAsync(request);

        // Assert
        result1.Value.ApiKey.Should().NotBe(result2.Value.ApiKey);
        result1.Value.KeyId.Should().NotBe(result2.Value.KeyId);
    }

    #endregion

    #region Validate

    [Fact]
    public async Task ValidateAsync_WithValidKey_ReturnsValid()
    {
        // Arrange
        var request = new ApiKeyRequest
        {
            Name = "Valid Key",
            UserId = Guid.NewGuid()
        };
        var generated = await _sut.GenerateAsync(request);

        // Act
        var result = await _sut.ValidateAsync(generated.Value.ApiKey);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsValid.Should().BeTrue();
        result.Value.KeyInfo.Should().NotBeNull();
        result.Value.KeyInfo!.Name.Should().Be("Valid Key");
    }

    [Fact]
    public async Task ValidateAsync_WithInvalidKey_ReturnsInvalid()
    {
        // Arrange
        var invalidKey = "aegis_invalid_key_12345";

        // Act
        var result = await _sut.ValidateAsync(invalidKey);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsValid.Should().BeFalse();
        result.Value.ReasonCode.Should().Be(ApiKeyInvalidReason.NotFound);
    }

    [Fact]
    public async Task ValidateAsync_WithExpiredKey_ReturnsExpired()
    {
        // Arrange
        var request = new ApiKeyRequest
        {
            Name = "Expired Key",
            UserId = Guid.NewGuid(),
            ExpiresAt = DateTime.UtcNow.AddSeconds(-1) // Already expired
        };
        var generated = await _sut.GenerateAsync(request);

        // Act
        var result = await _sut.ValidateAsync(generated.Value.ApiKey);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsValid.Should().BeFalse();
        result.Value.ReasonCode.Should().Be(ApiKeyInvalidReason.Expired);
    }

    [Fact]
    public async Task ValidateAsync_WithRevokedKey_ReturnsRevoked()
    {
        // Arrange
        var request = new ApiKeyRequest
        {
            Name = "Revoked Key",
            UserId = Guid.NewGuid()
        };
        var generated = await _sut.GenerateAsync(request);
        await _sut.RevokeAsync(generated.Value.KeyId, "Testing revocation");

        // Act
        var result = await _sut.ValidateAsync(generated.Value.ApiKey);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsValid.Should().BeFalse();
        result.Value.ReasonCode.Should().Be(ApiKeyInvalidReason.Revoked);
    }

    [Fact]
    public async Task ValidateAsync_UpdatesLastUsedAt()
    {
        // Arrange
        var request = new ApiKeyRequest
        {
            Name = "Usage Tracking",
            UserId = Guid.NewGuid()
        };
        var generated = await _sut.GenerateAsync(request);

        // Act
        await _sut.ValidateAsync(generated.Value.ApiKey);
        var listed = await _sut.ListAsync(request.UserId, null);

        // Assert
        listed.Value.First().LastUsedAt.Should().NotBeNull();
        listed.Value.First().TotalRequests.Should().BeGreaterThan(0);
    }

    #endregion

    #region Revoke

    [Fact]
    public async Task RevokeAsync_RevokesKey()
    {
        // Arrange
        var request = new ApiKeyRequest
        {
            Name = "To Revoke",
            UserId = Guid.NewGuid()
        };
        var generated = await _sut.GenerateAsync(request);

        // Act
        var result = await _sut.RevokeAsync(generated.Value.KeyId, "Security concern");

        // Assert
        result.IsSuccess.Should().BeTrue();

        var validation = await _sut.ValidateAsync(generated.Value.ApiKey);
        validation.Value.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task RevokeAsync_NonexistentKey_ReturnsFailure()
    {
        // Arrange
        var nonexistentId = Guid.NewGuid();

        // Act
        var result = await _sut.RevokeAsync(nonexistentId, "Test");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region List

    [Fact]
    public async Task ListAsync_ReturnsUserKeys()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await _sut.GenerateAsync(new ApiKeyRequest { Name = "Key 1", UserId = userId });
        await _sut.GenerateAsync(new ApiKeyRequest { Name = "Key 2", UserId = userId });
        await _sut.GenerateAsync(new ApiKeyRequest { Name = "Other Key", UserId = Guid.NewGuid() });

        // Act
        var result = await _sut.ListAsync(userId, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().OnlyContain(k => k.UserId == userId);
    }

    [Fact]
    public async Task ListAsync_ExcludesRevokedByDefault()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var key1 = await _sut.GenerateAsync(new ApiKeyRequest { Name = "Active Key", UserId = userId });
        var key2 = await _sut.GenerateAsync(new ApiKeyRequest { Name = "Revoked Key", UserId = userId });
        await _sut.RevokeAsync(key2.Value.KeyId, "Test");

        // Act
        var result = await _sut.ListAsync(userId, null, includeRevoked: false);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value.First().Name.Should().Be("Active Key");
    }

    [Fact]
    public async Task ListAsync_IncludesRevokedWhenRequested()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await _sut.GenerateAsync(new ApiKeyRequest { Name = "Active Key", UserId = userId });
        var key2 = await _sut.GenerateAsync(new ApiKeyRequest { Name = "Revoked Key", UserId = userId });
        await _sut.RevokeAsync(key2.Value.KeyId, "Test");

        // Act
        var result = await _sut.ListAsync(userId, null, includeRevoked: true);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    #endregion

    #region Rotate

    [Fact]
    public async Task RotateAsync_RevokesOldAndCreatesNew()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var original = await _sut.GenerateAsync(new ApiKeyRequest { Name = "To Rotate", UserId = userId });

        // Act
        var result = await _sut.RotateAsync(original.Value.KeyId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ApiKey.Should().NotBe(original.Value.ApiKey);
        result.Value.Name.Should().Be("To Rotate");

        var oldValidation = await _sut.ValidateAsync(original.Value.ApiKey);
        oldValidation.Value.IsValid.Should().BeFalse();

        var newValidation = await _sut.ValidateAsync(result.Value.ApiKey);
        newValidation.Value.IsValid.Should().BeTrue();
    }

    #endregion

    #region Update

    [Fact]
    public async Task UpdateAsync_UpdatesKeyProperties()
    {
        // Arrange
        var request = new ApiKeyRequest
        {
            Name = "Original Name",
            UserId = Guid.NewGuid(),
            Scopes = new List<string> { "read" }
        };
        var generated = await _sut.GenerateAsync(request);

        var update = new ApiKeyUpdate
        {
            Name = "Updated Name",
            Scopes = new List<string> { "read", "write", "admin" }
        };

        // Act
        var result = await _sut.UpdateAsync(generated.Value.KeyId, update);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Updated Name");
        result.Value.Scopes.Should().Contain("admin");
    }

    [Fact]
    public async Task UpdateAsync_CanDisableKey()
    {
        // Arrange
        var request = new ApiKeyRequest
        {
            Name = "To Disable",
            UserId = Guid.NewGuid()
        };
        var generated = await _sut.GenerateAsync(request);

        // Act
        await _sut.UpdateAsync(generated.Value.KeyId, new ApiKeyUpdate { IsActive = false });
        var validation = await _sut.ValidateAsync(generated.Value.ApiKey);

        // Assert
        validation.Value.IsValid.Should().BeFalse();
        validation.Value.ReasonCode.Should().Be(ApiKeyInvalidReason.Disabled);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task GenerateAsync_WithEmptyName_ReturnsFailure()
    {
        // Arrange
        var request = new ApiKeyRequest
        {
            Name = "",
            UserId = Guid.NewGuid()
        };

        // Act
        var result = await _sut.GenerateAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithMalformedKey_ReturnsInvalidFormat()
    {
        // Act
        var result = await _sut.ValidateAsync("not_a_valid_key");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsValid.Should().BeFalse();
        result.Value.ReasonCode.Should().Be(ApiKeyInvalidReason.InvalidFormat);
    }

    [Fact]
    public async Task ValidateAsync_WithNullKey_ReturnsFailure()
    {
        // Act
        var result = await _sut.ValidateAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion
}
