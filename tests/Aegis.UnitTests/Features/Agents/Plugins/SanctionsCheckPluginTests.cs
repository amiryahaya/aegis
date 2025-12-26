using Aegis.Api.Features.Agents.Plugins;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Aegis.UnitTests.Features.Agents.Plugins;

public class SanctionsCheckPluginTests
{
    private readonly ILogger<SanctionsCheckPlugin> _logger;
    private readonly SanctionsCheckPlugin _plugin;

    public SanctionsCheckPluginTests()
    {
        _logger = Substitute.For<ILogger<SanctionsCheckPlugin>>();
        _plugin = new SanctionsCheckPlugin(_logger);
    }

    [Fact]
    public async Task CheckSanctionsAsync_WithValidEntity_ShouldReturnResult()
    {
        // Act
        var result = await _plugin.CheckSanctionsAsync("John Doe", "Person");

        // Assert
        result.Should().Contain("John Doe");
        result.Should().Contain("success");
    }

    [Fact]
    public async Task CheckSanctionsAsync_WithEmptyName_ShouldReturnError()
    {
        // Act
        var result = await _plugin.CheckSanctionsAsync("", "Person");

        // Assert
        result.Should().Contain("Entity name cannot be empty");
    }
}
