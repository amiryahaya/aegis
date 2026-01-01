using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.Security;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Aegis.UnitTests.Services.Security;

public class ContentFilterTests
{
    private readonly ILogger<ContentFilter> _logger;
    private readonly ContentFilter _sut;

    public ContentFilterTests()
    {
        _logger = Substitute.For<ILogger<ContentFilter>>();
        _sut = new ContentFilter(_logger);
    }

    #region PII Masking

    [Fact]
    public async Task FilterAsync_WithEmail_MasksEmail()
    {
        // Arrange
        var request = new ContentFilterRequest
        {
            Content = "Contact me at john.doe@example.com for more info.",
            MaskPii = true
        };

        // Act
        var result = await _sut.FilterAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.FilteredContent.Should().NotContain("john.doe@example.com");
        result.Value.FilteredContent.Should().Contain("[EMAIL]");
        result.Value.WasModified.Should().BeTrue();
        result.Value.AppliedFilters.Should().Contain(f => f.FilterType == "PII_EMAIL");
    }

    [Fact]
    public async Task FilterAsync_WithPhoneNumber_MasksPhone()
    {
        // Arrange
        var request = new ContentFilterRequest
        {
            Content = "Call me at 555-123-4567 or (555) 987-6543.",
            MaskPii = true
        };

        // Act
        var result = await _sut.FilterAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.FilteredContent.Should().NotContain("555-123-4567");
        result.Value.FilteredContent.Should().NotContain("(555) 987-6543");
        result.Value.FilteredContent.Should().Contain("[PHONE]");
    }

    [Fact]
    public async Task FilterAsync_WithSsn_MasksSsn()
    {
        // Arrange
        var request = new ContentFilterRequest
        {
            Content = "SSN: 123-45-6789",
            MaskPii = true
        };

        // Act
        var result = await _sut.FilterAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.FilteredContent.Should().NotContain("123-45-6789");
        result.Value.FilteredContent.Should().Contain("[SSN]");
    }

    [Fact]
    public async Task FilterAsync_WithCreditCard_MasksCreditCard()
    {
        // Arrange
        var request = new ContentFilterRequest
        {
            Content = "Card number: 4111-1111-1111-1111",
            MaskPii = true
        };

        // Act
        var result = await _sut.FilterAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.FilteredContent.Should().NotContain("4111-1111-1111-1111");
        result.Value.FilteredContent.Should().Contain("[CREDIT_CARD]");
    }

    [Fact]
    public async Task FilterAsync_WithPiiDisabled_PreservesPii()
    {
        // Arrange
        var request = new ContentFilterRequest
        {
            Content = "Contact: john@example.com",
            MaskPii = false
        };

        // Act
        var result = await _sut.FilterAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.FilteredContent.Should().Contain("john@example.com");
    }

    #endregion

    #region Credential Masking

    [Fact]
    public async Task FilterAsync_WithApiKey_MasksCredential()
    {
        // Arrange
        var request = new ContentFilterRequest
        {
            Content = "API_KEY=sk-abcdefghijklmnopqrstuvwxyz",
            MaskCredentials = true,
            MaskPii = false  // Disable PII masking to avoid phone number detection
        };

        // Act
        var result = await _sut.FilterAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.FilteredContent.Should().NotContain("sk-abcdefghijklmnopqrstuvwxyz");
        result.Value.FilteredContent.Should().Contain("[API_KEY]");
    }

    [Fact]
    public async Task FilterAsync_WithPassword_MasksPassword()
    {
        // Arrange
        var request = new ContentFilterRequest
        {
            Content = "password: MySecretPass123!",
            MaskCredentials = true
        };

        // Act
        var result = await _sut.FilterAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.FilteredContent.Should().NotContain("MySecretPass123!");
        result.Value.FilteredContent.Should().Contain("[PASSWORD]");
    }

    [Fact]
    public async Task FilterAsync_WithConnectionString_MasksCredentials()
    {
        // Arrange
        var request = new ContentFilterRequest
        {
            Content = "Server=localhost;Database=mydb;User=admin;Password=secret123",
            MaskCredentials = true
        };

        // Act
        var result = await _sut.FilterAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.FilteredContent.Should().NotContain("secret123");
    }

    #endregion

    #region Profanity Filtering

    [Fact]
    public async Task FilterAsync_WithProfanity_FiltersProfanity()
    {
        // Arrange
        var request = new ContentFilterRequest
        {
            Content = "This is damn annoying",
            FilterProfanity = true
        };

        // Act
        var result = await _sut.FilterAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.WasModified.Should().BeTrue();
        result.Value.AppliedFilters.Should().Contain(f => f.FilterType == "PROFANITY");
    }

    [Fact]
    public async Task FilterAsync_WithProfanityDisabled_PreservesProfanity()
    {
        // Arrange
        var request = new ContentFilterRequest
        {
            Content = "This is damn annoying",
            FilterProfanity = false
        };

        // Act
        var result = await _sut.FilterAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.FilteredContent.Should().Contain("damn");
    }

    #endregion

    #region Custom Rules

    [Fact]
    public async Task FilterAsync_WithCustomMaskRule_AppliesRule()
    {
        // Arrange
        var request = new ContentFilterRequest
        {
            Content = "Project code: PROJ-12345",
            CustomRules = new List<FilterRule>
            {
                new FilterRule
                {
                    Id = "project_code",
                    Pattern = @"PROJ-\d+",
                    Action = FilterAction.Mask,
                    Replacement = "[PROJECT]"
                }
            }
        };

        // Act
        var result = await _sut.FilterAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.FilteredContent.Should().Contain("[PROJECT]");
        result.Value.FilteredContent.Should().NotContain("PROJ-12345");
    }

    [Fact]
    public async Task FilterAsync_WithCustomRemoveRule_RemovesContent()
    {
        // Arrange
        var request = new ContentFilterRequest
        {
            Content = "Text with CONFIDENTIAL marker here",
            CustomRules = new List<FilterRule>
            {
                new FilterRule
                {
                    Id = "confidential",
                    Pattern = @"CONFIDENTIAL",
                    Action = FilterAction.Remove
                }
            }
        };

        // Act
        var result = await _sut.FilterAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.FilteredContent.Should().NotContain("CONFIDENTIAL");
    }

    [Fact]
    public async Task FilterAsync_WithBlockRule_BlocksContent()
    {
        // Arrange
        var request = new ContentFilterRequest
        {
            Content = "This contains BLOCKED_WORD content",
            CustomRules = new List<FilterRule>
            {
                new FilterRule
                {
                    Id = "blocked",
                    Pattern = @"BLOCKED_WORD",
                    Action = FilterAction.Block
                }
            }
        };

        // Act
        var result = await _sut.FilterAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.WasBlocked.Should().BeTrue();
        result.Value.BlockReason.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Policy Checking

    [Fact]
    public async Task CheckPoliciesAsync_WithNoViolations_Passes()
    {
        // Arrange
        var content = "This is safe content with no issues.";
        var policies = new List<ContentPolicy>
        {
            new ContentPolicy
            {
                Id = "pii-policy",
                Name = "PII Policy",
                Categories = new List<SensitiveContentCategory> { SensitiveContentCategory.PersonalInfo }
            }
        };

        // Act
        var result = await _sut.CheckPoliciesAsync(content, policies);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Passed.Should().BeTrue();
        result.Value.Violations.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckPoliciesAsync_WithPiiViolation_ReportsViolation()
    {
        // Arrange
        var content = "SSN: 123-45-6789";
        var policies = new List<ContentPolicy>
        {
            new ContentPolicy
            {
                Id = "pii-policy",
                Name = "PII Policy",
                Categories = new List<SensitiveContentCategory> { SensitiveContentCategory.PersonalInfo },
                SeverityThreshold = 0.1,  // Lower threshold to catch the SSN
                Action = PolicyAction.Block
            }
        };

        // Act
        var result = await _sut.CheckPoliciesAsync(content, policies);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Passed.Should().BeFalse();
        result.Value.Violations.Should().NotBeEmpty();
        result.Value.RecommendedAction.Should().Be(PolicyAction.Block);
    }

    #endregion

    #region Safety Scoring

    [Fact]
    public async Task FilterAsync_WithSafeContent_ReturnsHighSafetyScore()
    {
        // Arrange
        var request = new ContentFilterRequest
        {
            Content = "The weather is nice today."
        };

        // Act
        var result = await _sut.FilterAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SafetyScore.Should().BeGreaterOrEqualTo(0.9);
    }

    [Fact]
    public async Task FilterAsync_WithMultipleSensitiveCategories_ReturnsLowerSafetyScore()
    {
        // Arrange
        var request = new ContentFilterRequest
        {
            Content = "SSN: 123-45-6789, password: secret123",
            MaskPii = true,
            MaskCredentials = true
        };

        // Act
        var result = await _sut.FilterAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SafetyScore.Should().BeLessThan(0.9);
        result.Value.DetectedCategories.Should().HaveCountGreaterOrEqualTo(1);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task FilterAsync_WithEmptyContent_ReturnsEmpty()
    {
        // Arrange
        var request = new ContentFilterRequest
        {
            Content = ""
        };

        // Act
        var result = await _sut.FilterAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.FilteredContent.Should().BeEmpty();
        result.Value.WasModified.Should().BeFalse();
    }

    [Fact]
    public async Task FilterAsync_WithNullContent_ReturnsFailure()
    {
        // Arrange
        var request = new ContentFilterRequest
        {
            Content = null!
        };

        // Act
        var result = await _sut.FilterAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task FilterAsync_WithMultipleEmails_MasksAll()
    {
        // Arrange
        var request = new ContentFilterRequest
        {
            Content = "Contact alice@test.com or bob@test.com",
            MaskPii = true
        };

        // Act
        var result = await _sut.FilterAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.FilteredContent.Should().NotContain("alice@test.com");
        result.Value.FilteredContent.Should().NotContain("bob@test.com");
        result.Value.AppliedFilters.First(f => f.FilterType == "PII_EMAIL").MatchCount.Should().Be(2);
    }

    #endregion
}
