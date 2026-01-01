using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Aegis.UnitTests.Services.Configuration;

public class FeatureFlagServiceTests
{
    private readonly ILogger<InMemoryFeatureFlagService> _logger;
    private readonly InMemoryFeatureFlagService _sut;

    public FeatureFlagServiceTests()
    {
        _logger = Substitute.For<ILogger<InMemoryFeatureFlagService>>();
        _sut = new InMemoryFeatureFlagService(_logger);
    }

    [Fact]
    public async Task CreateAsync_WithValidRequest_ShouldCreateFlag()
    {
        // Arrange
        var request = new CreateFeatureFlagRequest
        {
            Key = "new-feature",
            Name = "New Feature",
            Description = "A new feature flag",
            IsEnabled = true
        };

        // Act
        var result = await _sut.CreateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Key.Should().Be("new-feature");
        result.Value.Name.Should().Be("New Feature");
        result.Value.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateKey_ShouldReturnConflict()
    {
        // Arrange
        var request = new CreateFeatureFlagRequest
        {
            Key = "duplicate-key",
            Name = "First Flag"
        };
        await _sut.CreateAsync(request);

        var duplicateRequest = new CreateFeatureFlagRequest
        {
            Key = "duplicate-key",
            Name = "Second Flag"
        };

        // Act
        var result = await _sut.CreateAsync(duplicateRequest);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Contain("KeyExists");
    }

    [Fact]
    public async Task CreateAsync_WithInvalidKey_ShouldReturnValidationError()
    {
        // Arrange
        var request = new CreateFeatureFlagRequest
        {
            Key = "123-invalid",
            Name = "Invalid Flag"
        };

        // Act
        var result = await _sut.CreateAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Contain("InvalidKey");
    }

    [Fact]
    public async Task IsEnabledAsync_WhenFlagEnabled_ShouldReturnTrue()
    {
        // Arrange
        var request = new CreateFeatureFlagRequest
        {
            Key = "enabled-flag",
            Name = "Enabled Flag",
            IsEnabled = true
        };
        await _sut.CreateAsync(request);

        // Act
        var result = await _sut.IsEnabledAsync("enabled-flag", FeatureFlagContext.Empty);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task IsEnabledAsync_WhenFlagDisabled_ShouldReturnFalse()
    {
        // Arrange
        var request = new CreateFeatureFlagRequest
        {
            Key = "disabled-flag",
            Name = "Disabled Flag",
            IsEnabled = false
        };
        await _sut.CreateAsync(request);

        // Act
        var result = await _sut.IsEnabledAsync("disabled-flag", FeatureFlagContext.Empty);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabledAsync_WhenFlagNotFound_ShouldReturnFalse()
    {
        // Act
        var result = await _sut.IsEnabledAsync("non-existent", FeatureFlagContext.Empty);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabledAsync_WithUserRule_ShouldEvaluateCorrectly()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var request = new CreateFeatureFlagRequest
        {
            Key = "user-specific",
            Name = "User Specific Flag",
            IsEnabled = false,
            Rules =
            [
                new FeatureFlagRule
                {
                    Name = "Enable for specific user",
                    Priority = 1,
                    Conditions =
                    [
                        new FeatureFlagCondition
                        {
                            Attribute = "userId",
                            Operator = FeatureFlagOperator.Equals,
                            Value = userId.ToString()
                        }
                    ],
                    Action = FeatureFlagRuleAction.Enable
                }
            ]
        };
        await _sut.CreateAsync(request);

        // Act
        var resultForUser = await _sut.IsEnabledAsync("user-specific",
            FeatureFlagContext.ForUser(userId));
        var resultForOther = await _sut.IsEnabledAsync("user-specific",
            FeatureFlagContext.ForUser(Guid.CreateVersion7()));

        // Assert
        resultForUser.Value.Should().BeTrue();
        resultForOther.Value.Should().BeFalse();
    }

    [Fact]
    public async Task GetVariantAsync_ShouldReturnCorrectVariant()
    {
        // Arrange
        var request = new CreateFeatureFlagRequest
        {
            Key = "variant-flag",
            Name = "Variant Flag",
            Type = FeatureFlagType.String,
            IsEnabled = true,
            DefaultValue = "default",
            Rules =
            [
                new FeatureFlagRule
                {
                    Name = "Set variant",
                    Priority = 1,
                    Action = FeatureFlagRuleAction.SetVariant,
                    VariantKey = "variant-a"
                }
            ],
            Variants =
            [
                new FeatureFlagVariant { Key = "variant-a", Name = "Variant A", Value = "value-a" },
                new FeatureFlagVariant { Key = "variant-b", Name = "Variant B", Value = "value-b" }
            ]
        };
        await _sut.CreateAsync(request);

        // Act
        var result = await _sut.GetVariantAsync<string>("variant-flag", "fallback",
            FeatureFlagContext.Empty);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("value-a");
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateFlag()
    {
        // Arrange
        var createRequest = new CreateFeatureFlagRequest
        {
            Key = "update-test",
            Name = "Original Name",
            IsEnabled = false
        };
        var createResult = await _sut.CreateAsync(createRequest);

        var updateRequest = new UpdateFeatureFlagRequest
        {
            Name = "Updated Name",
            IsEnabled = true
        };

        // Act
        var result = await _sut.UpdateAsync(createResult.Value.Id, updateRequest);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Updated Name");
        result.Value.IsEnabled.Should().BeTrue();
        result.Value.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveFlag()
    {
        // Arrange
        var request = new CreateFeatureFlagRequest
        {
            Key = "delete-test",
            Name = "Delete Test"
        };
        var createResult = await _sut.CreateAsync(request);

        // Act
        var deleteResult = await _sut.DeleteAsync(createResult.Value.Id);
        var getResult = await _sut.GetByIdAsync(createResult.Value.Id);

        // Assert
        deleteResult.IsSuccess.Should().BeTrue();
        getResult.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task GetByKeyAsync_ShouldReturnFlag()
    {
        // Arrange
        var request = new CreateFeatureFlagRequest
        {
            Key = "get-by-key-test",
            Name = "Get By Key Test"
        };
        await _sut.CreateAsync(request);

        // Act
        var result = await _sut.GetByKeyAsync("get-by-key-test");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Key.Should().Be("get-by-key-test");
    }

    [Fact]
    public async Task ListAsync_ShouldReturnAllFlags()
    {
        // Arrange
        await _sut.CreateAsync(new CreateFeatureFlagRequest { Key = "list-test-1", Name = "Test 1" });
        await _sut.CreateAsync(new CreateFeatureFlagRequest { Key = "list-test-2", Name = "Test 2" });

        // Act
        var result = await _sut.ListAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task ListAsync_WithFilter_ShouldReturnFilteredFlags()
    {
        // Arrange
        await _sut.CreateAsync(new CreateFeatureFlagRequest
        {
            Key = "filter-test-1",
            Name = "Test 1",
            IsEnabled = true
        });
        await _sut.CreateAsync(new CreateFeatureFlagRequest
        {
            Key = "filter-test-2",
            Name = "Test 2",
            IsEnabled = false
        });

        // Act
        var result = await _sut.ListAsync(new FeatureFlagFilter { IsEnabled = true });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().OnlyContain(f => f.IsEnabled);
    }

    [Fact]
    public async Task GetAuditHistoryAsync_ShouldReturnHistory()
    {
        // Arrange
        var createRequest = new CreateFeatureFlagRequest
        {
            Key = "audit-test",
            Name = "Audit Test"
        };
        var createResult = await _sut.CreateAsync(createRequest);

        await _sut.UpdateAsync(createResult.Value.Id, new UpdateFeatureFlagRequest
        {
            IsEnabled = true
        });

        // Act
        var result = await _sut.GetAuditHistoryAsync(createResult.Value.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task IsEnabledAsync_WithRolloutPercentage_ShouldBeDeterministic()
    {
        // Arrange
        var request = new CreateFeatureFlagRequest
        {
            Key = "rollout-test",
            Name = "Rollout Test",
            IsEnabled = false,
            Rules =
            [
                new FeatureFlagRule
                {
                    Name = "50% Rollout",
                    Priority = 1,
                    Action = FeatureFlagRuleAction.Rollout,
                    RolloutPercentage = 50
                }
            ]
        };
        await _sut.CreateAsync(request);

        var userId = Guid.CreateVersion7();
        var context = FeatureFlagContext.ForUser(userId);

        // Act - Call multiple times with same context
        var result1 = await _sut.IsEnabledAsync("rollout-test", context);
        var result2 = await _sut.IsEnabledAsync("rollout-test", context);

        // Assert - Same user should get same result (deterministic)
        result1.Value.Should().Be(result2.Value);
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrow()
    {
        // Act
        var act = () => new InMemoryFeatureFlagService(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public async Task IsEnabledAsync_WithContainsOperator_ShouldEvaluateCorrectly()
    {
        // Arrange
        var request = new CreateFeatureFlagRequest
        {
            Key = "contains-test",
            Name = "Contains Test",
            IsEnabled = false,
            Rules =
            [
                new FeatureFlagRule
                {
                    Name = "Enable for beta users",
                    Priority = 1,
                    Conditions =
                    [
                        new FeatureFlagCondition
                        {
                            Attribute = "email",
                            Operator = FeatureFlagOperator.Contains,
                            Value = "@beta.com"
                        }
                    ],
                    Action = FeatureFlagRuleAction.Enable
                }
            ]
        };
        await _sut.CreateAsync(request);

        // Act
        var betaUser = new FeatureFlagContext
        {
            Attributes = new Dictionary<string, string> { ["email"] = "user@beta.com" }
        };
        var regularUser = new FeatureFlagContext
        {
            Attributes = new Dictionary<string, string> { ["email"] = "user@example.com" }
        };

        var betaResult = await _sut.IsEnabledAsync("contains-test", betaUser);
        var regularResult = await _sut.IsEnabledAsync("contains-test", regularUser);

        // Assert
        betaResult.Value.Should().BeTrue();
        regularResult.Value.Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabledAsync_WithInOperator_ShouldEvaluateCorrectly()
    {
        // Arrange
        var teamId1 = Guid.CreateVersion7();
        var teamId2 = Guid.CreateVersion7();

        var request = new CreateFeatureFlagRequest
        {
            Key = "in-test",
            Name = "In Test",
            IsEnabled = false,
            Rules =
            [
                new FeatureFlagRule
                {
                    Name = "Enable for specific teams",
                    Priority = 1,
                    Conditions =
                    [
                        new FeatureFlagCondition
                        {
                            Attribute = "teamId",
                            Operator = FeatureFlagOperator.In,
                            Value = $"{teamId1},{teamId2}"
                        }
                    ],
                    Action = FeatureFlagRuleAction.Enable
                }
            ]
        };
        await _sut.CreateAsync(request);

        // Act
        var team1Result = await _sut.IsEnabledAsync("in-test", FeatureFlagContext.ForTeam(teamId1));
        var otherResult = await _sut.IsEnabledAsync("in-test", FeatureFlagContext.ForTeam(Guid.CreateVersion7()));

        // Assert
        team1Result.Value.Should().BeTrue();
        otherResult.Value.Should().BeFalse();
    }
}
