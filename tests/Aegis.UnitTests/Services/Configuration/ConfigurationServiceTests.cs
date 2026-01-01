using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Aegis.UnitTests.Services.Configuration;

public class ConfigurationServiceTests
{
    private readonly ILogger<InMemoryConfigurationService> _logger;
    private readonly InMemoryConfigurationService _sut;

    public ConfigurationServiceTests()
    {
        _logger = Substitute.For<ILogger<InMemoryConfigurationService>>();
        _sut = new InMemoryConfigurationService(_logger);
    }

    [Fact]
    public async Task GetAsync_WithDefaultKey_ShouldReturnDefaultValue()
    {
        // Act
        var result = await _sut.GetAsync<bool>(ConfigurationKeys.CacheEnabled, false);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue(); // Default is true
    }

    [Fact]
    public async Task SetAsync_ShouldUpdateValue()
    {
        // Act
        var setResult = await _sut.SetAsync(ConfigurationKeys.CacheEnabled, false);
        var getResult = await _sut.GetAsync<bool>(ConfigurationKeys.CacheEnabled, true);

        // Assert
        setResult.IsSuccess.Should().BeTrue();
        getResult.Value.Should().BeFalse();
    }

    [Fact]
    public async Task GetAsync_WithCategory_ShouldReturnValue()
    {
        // Arrange
        await _sut.SetAsync(ConfigurationCategory.Llm, "customKey", "customValue");

        // Act
        var result = await _sut.GetAsync<string>(ConfigurationCategory.Llm, "customKey", "default");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("customValue");
    }

    [Fact]
    public async Task GetByCategoryAsync_ShouldReturnOnlyCategoryEntries()
    {
        // Act
        var result = await _sut.GetByCategoryAsync(ConfigurationCategory.Cache);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().OnlyContain(e => e.Category == ConfigurationCategory.Cache);
        result.Value.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllEntries()
    {
        // Act
        var result = await _sut.GetAllAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCountGreaterThan(0);
        result.Value.Should().Contain(e => e.Category == ConfigurationCategory.Llm);
        result.Value.Should().Contain(e => e.Category == ConfigurationCategory.Cache);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveEntry()
    {
        // Arrange
        await _sut.SetAsync("custom.key", "value");

        // Act
        var deleteResult = await _sut.DeleteAsync("custom.key");
        var getResult = await _sut.GetAsync<string>("custom.key", "default");

        // Assert
        deleteResult.IsSuccess.Should().BeTrue();
        getResult.Value.Should().Be("default");
    }

    [Fact]
    public async Task DeleteAsync_WhenNotExists_ShouldReturnNotFound()
    {
        // Act
        var result = await _sut.DeleteAsync("non-existent-key");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Contain("NotFound");
    }

    [Fact]
    public async Task ResetCategoryAsync_ShouldResetToDefaults()
    {
        // Arrange
        await _sut.SetAsync(ConfigurationKeys.CacheDefaultTtl, 7200);
        await _sut.SetAsync(ConfigurationKeys.CacheMaxSize, 50000);

        // Act
        var result = await _sut.ResetCategoryAsync(ConfigurationCategory.Cache);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var ttl = await _sut.GetAsync<int>(ConfigurationKeys.CacheDefaultTtl, 0);
        var maxSize = await _sut.GetAsync<int>(ConfigurationKeys.CacheMaxSize, 0);

        ttl.Value.Should().Be(3600); // Default
        maxSize.Value.Should().Be(10000); // Default
    }

    [Fact]
    public async Task GetAuditHistoryAsync_ShouldReturnChanges()
    {
        // Arrange
        await _sut.SetAsync(ConfigurationKeys.CacheEnabled, false, reason: "Testing");
        await _sut.SetAsync(ConfigurationKeys.CacheEnabled, true, reason: "Re-enabled");

        // Act
        var result = await _sut.GetAuditHistoryAsync(key: ConfigurationKeys.CacheEnabled);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetAuditHistoryAsync_FilterByCategory_ShouldWork()
    {
        // Arrange
        await _sut.SetAsync(ConfigurationKeys.CacheEnabled, false);
        await _sut.SetAsync(ConfigurationKeys.LlmMaxTokens, 8192);

        // Act
        var result = await _sut.GetAuditHistoryAsync(category: ConfigurationCategory.Cache);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().OnlyContain(e => e.Category == ConfigurationCategory.Cache);
    }

    [Fact]
    public async Task ImportAsync_ShouldImportNewEntries()
    {
        // Arrange
        var config = new Dictionary<string, object>
        {
            ["import.key1"] = "value1",
            ["import.key2"] = 42
        };

        // Act
        var result = await _sut.ImportAsync(config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(2);

        var key1 = await _sut.GetAsync<string>("import.key1", "default");
        var key2 = await _sut.GetAsync<int>("import.key2", 0);

        key1.Value.Should().Be("value1");
        key2.Value.Should().Be(42);
    }

    [Fact]
    public async Task ImportAsync_WithoutOverwrite_ShouldNotReplaceExisting()
    {
        // Arrange
        await _sut.SetAsync("existing.key", "original");

        var config = new Dictionary<string, object>
        {
            ["existing.key"] = "new"
        };

        // Act
        var result = await _sut.ImportAsync(config, overwrite: false);

        // Assert
        result.Value.Should().Be(0);

        var value = await _sut.GetAsync<string>("existing.key", "default");
        value.Value.Should().Be("original");
    }

    [Fact]
    public async Task ImportAsync_WithOverwrite_ShouldReplaceExisting()
    {
        // Arrange
        await _sut.SetAsync("existing.key", "original");

        var config = new Dictionary<string, object>
        {
            ["existing.key"] = "new"
        };

        // Act
        var result = await _sut.ImportAsync(config, overwrite: true);

        // Assert
        result.Value.Should().Be(1);

        var value = await _sut.GetAsync<string>("existing.key", "default");
        value.Value.Should().Be("new");
    }

    [Fact]
    public async Task ExportAsync_ShouldReturnAllNonSecretValues()
    {
        // Act
        var result = await _sut.ExportAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCountGreaterThan(0);
        result.Value.Should().ContainKey(ConfigurationKeys.CacheEnabled);
    }

    [Fact]
    public async Task ExportAsync_FilterByCategory_ShouldWork()
    {
        // Act
        var result = await _sut.ExportAsync(ConfigurationCategory.Llm);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Keys.Should().OnlyContain(k => k.StartsWith("llm."));
    }

    [Fact]
    public async Task ValidateAsync_ShouldPassForValidValues()
    {
        // Act
        var result = await _sut.ValidateAsync(ConfigurationKeys.CacheEnabled, true);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task SetAsync_WithAuditInfo_ShouldRecordChangedBy()
    {
        // Arrange
        var userId = Guid.CreateVersion7();

        // Act
        await _sut.SetAsync(ConfigurationKeys.CacheEnabled, false, changedBy: userId, reason: "Testing");

        // Assert
        var history = await _sut.GetAuditHistoryAsync(key: ConfigurationKeys.CacheEnabled, limit: 1);
        history.Value.First().ChangedBy.Should().Be(userId);
        history.Value.First().Reason.Should().Be("Testing");
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrow()
    {
        // Act
        var act = () => new InMemoryConfigurationService(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public async Task Constructor_ShouldInitializeDefaults()
    {
        // Act
        var result = await _sut.GetAllAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain(e => e.Key == ConfigurationKeys.LlmDefaultModel);
        result.Value.Should().Contain(e => e.Key == ConfigurationKeys.CacheDefaultTtl);
        result.Value.Should().Contain(e => e.Key == ConfigurationKeys.RateLimitEnabled);
    }

    [Fact]
    public async Task GetAsync_WithNumericTypes_ShouldConvertCorrectly()
    {
        // Arrange
        await _sut.SetAsync("test.int", 42);
        await _sut.SetAsync("test.double", 3.14);

        // Act
        var intResult = await _sut.GetAsync<int>("test.int", 0);
        var doubleResult = await _sut.GetAsync<double>("test.double", 0.0);

        // Assert
        intResult.Value.Should().Be(42);
        doubleResult.Value.Should().Be(3.14);
    }

    [Fact]
    public async Task ConfigurationDefaults_GetCategory_ShouldReturnCorrectCategory()
    {
        // Assert
        ConfigurationDefaults.GetCategory(ConfigurationKeys.LlmDefaultModel)
            .Should().Be(ConfigurationCategory.Llm);
        ConfigurationDefaults.GetCategory(ConfigurationKeys.CacheEnabled)
            .Should().Be(ConfigurationCategory.Cache);
        ConfigurationDefaults.GetCategory(ConfigurationKeys.RateLimitEnabled)
            .Should().Be(ConfigurationCategory.RateLimit);
        ConfigurationDefaults.GetCategory(ConfigurationKeys.WebhookEnabled)
            .Should().Be(ConfigurationCategory.Webhook);
        ConfigurationDefaults.GetCategory(ConfigurationKeys.SecurityRequireMfa)
            .Should().Be(ConfigurationCategory.Security);
    }

    [Fact]
    public async Task GetAuditHistoryAsync_WithLimit_ShouldRespectLimit()
    {
        // Arrange
        for (int i = 0; i < 10; i++)
        {
            await _sut.SetAsync("audit.test", i);
        }

        // Act
        var result = await _sut.GetAuditHistoryAsync(key: "audit.test", limit: 5);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(5);
    }
}
