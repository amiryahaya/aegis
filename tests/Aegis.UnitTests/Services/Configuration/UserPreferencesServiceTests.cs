using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Aegis.UnitTests.Services.Configuration;

public class UserPreferencesServiceTests
{
    private readonly ILogger<InMemoryUserPreferencesService> _logger;
    private readonly InMemoryUserPreferencesService _sut;

    public UserPreferencesServiceTests()
    {
        _logger = Substitute.For<ILogger<InMemoryUserPreferencesService>>();
        _sut = new InMemoryUserPreferencesService(_logger);
    }

    [Fact]
    public async Task SetAsync_ShouldStorePreference()
    {
        // Arrange
        var userId = Guid.CreateVersion7();

        // Act
        var result = await _sut.SetAsync(userId, PreferenceKeys.UiTheme, "dark");

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GetAsync_WhenPreferenceExists_ShouldReturnValue()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        await _sut.SetAsync(userId, PreferenceKeys.UiTheme, "dark");

        // Act
        var result = await _sut.GetAsync<string>(userId, PreferenceKeys.UiTheme, "light");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("dark");
    }

    [Fact]
    public async Task GetAsync_WhenPreferenceNotExists_ShouldReturnDefault()
    {
        // Arrange
        var userId = Guid.CreateVersion7();

        // Act
        var result = await _sut.GetAsync<string>(userId, PreferenceKeys.UiTheme, "light");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("light");
    }

    [Fact]
    public async Task SetWorkspaceAsync_ShouldStoreWorkspacePreference()
    {
        // Arrange
        var workspaceId = Guid.CreateVersion7();

        // Act
        var result = await _sut.SetWorkspaceAsync(workspaceId, PreferenceKeys.DataChunkSize, 1024);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GetWorkspaceAsync_WhenPreferenceExists_ShouldReturnValue()
    {
        // Arrange
        var workspaceId = Guid.CreateVersion7();
        await _sut.SetWorkspaceAsync(workspaceId, PreferenceKeys.DataChunkSize, 1024);

        // Act
        var result = await _sut.GetWorkspaceAsync<int>(workspaceId, PreferenceKeys.DataChunkSize, 512);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1024);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllUserPreferences()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        await _sut.SetAsync(userId, PreferenceKeys.UiTheme, "dark");
        await _sut.SetAsync(userId, PreferenceKeys.QueryTemperature, 0.8);

        // Act
        var result = await _sut.GetAllAsync(userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(userId);
        result.Value.Ui.Theme.Should().Be("dark");
        result.Value.Query.Temperature.Should().Be(0.8);
    }

    [Fact]
    public async Task GetAllWorkspaceAsync_ShouldReturnAllWorkspacePreferences()
    {
        // Arrange
        var workspaceId = Guid.CreateVersion7();
        await _sut.SetWorkspaceAsync(workspaceId, PreferenceKeys.DataChunkSize, 1024);
        await _sut.SetWorkspaceAsync(workspaceId, PreferenceKeys.DataEnableVersioning, false);

        // Act
        var result = await _sut.GetAllWorkspaceAsync(workspaceId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.WorkspaceId.Should().Be(workspaceId);
        result.Value.Data.ChunkSize.Should().Be(1024);
        result.Value.Data.EnableVersioning.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateMultiplePreferences()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var preferences = new Dictionary<string, object>
        {
            [PreferenceKeys.UiTheme] = "dark",
            [PreferenceKeys.UiCompactMode] = true,
            [PreferenceKeys.QueryMaxResults] = 20
        };

        // Act
        var result = await _sut.UpdateAsync(userId, preferences);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var theme = await _sut.GetAsync<string>(userId, PreferenceKeys.UiTheme, "light");
        var compact = await _sut.GetAsync<bool>(userId, PreferenceKeys.UiCompactMode, false);
        var maxResults = await _sut.GetAsync<int>(userId, PreferenceKeys.QueryMaxResults, 10);

        theme.Value.Should().Be("dark");
        compact.Value.Should().BeTrue();
        maxResults.Value.Should().Be(20);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemovePreference()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        await _sut.SetAsync(userId, PreferenceKeys.UiTheme, "dark");

        // Act
        var deleteResult = await _sut.DeleteAsync(userId, PreferenceKeys.UiTheme);
        var getResult = await _sut.GetAsync<string>(userId, PreferenceKeys.UiTheme, "light");

        // Assert
        deleteResult.IsSuccess.Should().BeTrue();
        getResult.Value.Should().Be("light"); // Returns default
    }

    [Fact]
    public async Task ResetToDefaultsAsync_ShouldClearAllPreferences()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        await _sut.SetAsync(userId, PreferenceKeys.UiTheme, "dark");
        await _sut.SetAsync(userId, PreferenceKeys.QueryTemperature, 0.9);

        // Act
        var resetResult = await _sut.ResetToDefaultsAsync(userId);
        var allPrefs = await _sut.GetAllAsync(userId);

        // Assert
        resetResult.IsSuccess.Should().BeTrue();
        allPrefs.Value.Ui.Theme.Should().Be("system"); // Default value
        allPrefs.Value.Query.Temperature.Should().Be(0.7); // Default value
    }

    [Fact]
    public async Task GetEffectiveAsync_ShouldPrioritizeWorkspaceOverUser()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var workspaceId = Guid.CreateVersion7();

        await _sut.SetAsync(userId, PreferenceKeys.QueryMaxResults, 10);
        await _sut.SetWorkspaceAsync(workspaceId, PreferenceKeys.QueryMaxResults, 50);

        // Act
        var result = await _sut.GetEffectiveAsync<int>(userId, workspaceId,
            PreferenceKeys.QueryMaxResults, 5);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(50); // Workspace takes priority
    }

    [Fact]
    public async Task GetEffectiveAsync_ShouldFallbackToUser_WhenNoWorkspacePref()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var workspaceId = Guid.CreateVersion7();

        await _sut.SetAsync(userId, PreferenceKeys.QueryMaxResults, 10);

        // Act
        var result = await _sut.GetEffectiveAsync<int>(userId, workspaceId,
            PreferenceKeys.QueryMaxResults, 5);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(10); // User preference
    }

    [Fact]
    public async Task GetEffectiveAsync_ShouldReturnDefault_WhenNoPrefs()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        var workspaceId = Guid.CreateVersion7();

        // Act
        var result = await _sut.GetEffectiveAsync<int>(userId, workspaceId,
            PreferenceKeys.QueryMaxResults, 5);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(5); // Default value
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnDefaultsForNewUser()
    {
        // Arrange
        var userId = Guid.CreateVersion7();

        // Act
        var result = await _sut.GetAllAsync(userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Query.DefaultModel.Should().Be("gpt-4");
        result.Value.Ui.Theme.Should().Be("system");
        result.Value.Notifications.EmailEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task SetAsync_WithDifferentTypes_ShouldHandleCorrectly()
    {
        // Arrange
        var userId = Guid.CreateVersion7();

        // Act
        await _sut.SetAsync(userId, "custom.string", "test");
        await _sut.SetAsync(userId, "custom.int", 42);
        await _sut.SetAsync(userId, "custom.bool", true);
        await _sut.SetAsync(userId, "custom.double", 3.14);

        // Assert
        var str = await _sut.GetAsync<string>(userId, "custom.string", "default");
        var num = await _sut.GetAsync<int>(userId, "custom.int", 0);
        var boolean = await _sut.GetAsync<bool>(userId, "custom.bool", false);
        var dbl = await _sut.GetAsync<double>(userId, "custom.double", 0.0);

        str.Value.Should().Be("test");
        num.Value.Should().Be(42);
        boolean.Value.Should().BeTrue();
        dbl.Value.Should().Be(3.14);
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrow()
    {
        // Act
        var act = () => new InMemoryUserPreferencesService(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public async Task GetAllAsync_ShouldIncludeCustomPreferences()
    {
        // Arrange
        var userId = Guid.CreateVersion7();
        await _sut.SetAsync(userId, "custom.myPref", "myValue");

        // Act
        var result = await _sut.GetAllAsync(userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Custom.Should().ContainKey("custom.myPref");
        result.Value.Custom["custom.myPref"].Should().Be("myValue");
    }

    [Fact]
    public async Task UpdateWorkspaceAsync_ShouldUpdateMultiplePreferences()
    {
        // Arrange
        var workspaceId = Guid.CreateVersion7();
        var preferences = new Dictionary<string, object>
        {
            [PreferenceKeys.DataChunkSize] = 1024,
            [PreferenceKeys.DataRetentionDays] = 180
        };

        // Act
        var result = await _sut.UpdateWorkspaceAsync(workspaceId, preferences);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var allPrefs = await _sut.GetAllWorkspaceAsync(workspaceId);
        allPrefs.Value.Data.ChunkSize.Should().Be(1024);
        allPrefs.Value.Data.RetentionDays.Should().Be(180);
    }

    [Fact]
    public async Task DeleteWorkspaceAsync_ShouldRemovePreference()
    {
        // Arrange
        var workspaceId = Guid.CreateVersion7();
        await _sut.SetWorkspaceAsync(workspaceId, PreferenceKeys.DataChunkSize, 1024);

        // Act
        var deleteResult = await _sut.DeleteWorkspaceAsync(workspaceId, PreferenceKeys.DataChunkSize);
        var getResult = await _sut.GetWorkspaceAsync<int>(workspaceId, PreferenceKeys.DataChunkSize, 512);

        // Assert
        deleteResult.IsSuccess.Should().BeTrue();
        getResult.Value.Should().Be(512); // Returns default
    }
}
