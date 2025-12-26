using Aegis.Domain.Common;
using Aegis.Domain.Entities;
using Aegis.Domain.Repositories;
using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.Agents;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Aegis.UnitTests.Services.Agents;

public class PluginAuthorizationServiceTests
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<PluginAuthorizationService> _logger;
    private readonly PluginAuthorizationService _service;

    public PluginAuthorizationServiceTests()
    {
        _userRepository = Substitute.For<IUserRepository>();
        _logger = Substitute.For<ILogger<PluginAuthorizationService>>();
        _service = new PluginAuthorizationService(_userRepository, _logger);
    }

    [Fact]
    public async Task AuthorizePluginAsync_ViewerAccessingVectorSearch_ShouldBeAuthorized()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = User.Create("viewer@test.com", "Test Viewer", UserRole.Viewer);
        _userRepository.GetByIdAsync(userId).Returns(user);

        // Act
        var result = await _service.AuthorizePluginAsync(userId, "VectorSearchPlugin");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsAuthorized.Should().BeTrue();
        result.Value.PluginName.Should().Be("VectorSearchPlugin");
    }

    [Fact]
    public async Task AuthorizePluginAsync_ViewerAccessingSanctionsCheck_ShouldBeDenied()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = User.Create("viewer@test.com", "Test Viewer", UserRole.Viewer);
        _userRepository.GetByIdAsync(userId).Returns(user);

        // Act
        var result = await _service.AuthorizePluginAsync(userId, "SanctionsCheckPlugin");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsAuthorized.Should().BeFalse();
        result.Value.DenialReason.Should().Contain("not authorized");
    }

    [Fact]
    public async Task AuthorizePluginAsync_AnalystAccessingSanctionsCheck_ShouldBeAuthorized()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = User.Create("analyst@test.com", "Test Analyst", UserRole.Analyst);
        _userRepository.GetByIdAsync(userId).Returns(user);

        // Act
        var result = await _service.AuthorizePluginAsync(userId, "SanctionsCheckPlugin");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsAuthorized.Should().BeTrue();
    }

    [Fact]
    public async Task AuthorizePluginAsync_UnknownPlugin_ShouldBeDenied()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = User.Create("analyst@test.com", "Test Analyst", UserRole.Analyst);
        _userRepository.GetByIdAsync(userId).Returns(user);

        // Act
        var result = await _service.AuthorizePluginAsync(userId, "UnknownPlugin");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsAuthorized.Should().BeFalse();
        result.Value.DenialReason.Should().Contain("Unknown plugin");
    }

    [Fact]
    public async Task GetAvailablePluginsAsync_ForViewer_ShouldReturnBasicPlugins()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = User.Create("viewer@test.com", "Test Viewer", UserRole.Viewer);
        _userRepository.GetByIdAsync(userId).Returns(user);

        // Act
        var result = await _service.GetAvailablePluginsAsync(userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("VectorSearchPlugin");
        result.Value.Should().Contain("KeywordSearchPlugin");
        result.Value.Should().NotContain("SanctionsCheckPlugin");
    }

    [Fact]
    public async Task GetAvailablePluginsAsync_ForAnalyst_ShouldReturnAllPlugins()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = User.Create("analyst@test.com", "Test Analyst", UserRole.Analyst);
        _userRepository.GetByIdAsync(userId).Returns(user);

        // Act
        var result = await _service.GetAvailablePluginsAsync(userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(6); // All plugins
        result.Value.Should().Contain("SanctionsCheckPlugin");
        result.Value.Should().Contain("TimelineBuilderPlugin");
    }
}
