using Aegis.Api.Features.Agents.Plugins;
using Aegis.Domain.Common;
using Aegis.Domain.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Aegis.UnitTests.Features.Agents.Plugins;

public class KeywordSearchPluginTests
{
    private readonly IKeywordSearchService _searchService;
    private readonly ILogger<KeywordSearchPlugin> _logger;
    private readonly KeywordSearchPlugin _plugin;

    public KeywordSearchPluginTests()
    {
        _searchService = Substitute.For<IKeywordSearchService>();
        _logger = Substitute.For<ILogger<KeywordSearchPlugin>>();
        _plugin = new KeywordSearchPlugin(_searchService, _logger);
    }

    [Fact]
    public async Task KeywordSearchAsync_WithValidQuery_ShouldReturnResults()
    {
        // Arrange
        var query = "financial transactions investigation";
        var workspaceId = Guid.NewGuid();
        var expectedResults = new List<KeywordSearchResult>
        {
            new KeywordSearchResult
            {
                DocumentId = Guid.NewGuid(),
                Title = "Financial Report 2024",
                Content = "Analysis of financial transactions and investigations",
                BM25Score = 12.5f,
                MatchedTerms = new[] { "financial", "transactions", "investigation" }
            }
        };

        _searchService.SearchAsync(query, workspaceId, 10, Arg.Any<CancellationToken>())
            .Returns(Result<List<KeywordSearchResult>>.Success(expectedResults));

        // Act
        var result = await _plugin.KeywordSearchAsync(query, workspaceId.ToString(), 10);

        // Assert
        result.Should().Contain("Financial Report 2024");
        result.Should().Contain("12.5");
        result.Should().Contain("financial");
    }

    [Fact]
    public async Task KeywordSearchAsync_WhenSearchFails_ShouldReturnErrorMessage()
    {
        // Arrange
        var query = "test query";
        var workspaceId = Guid.NewGuid();
        var error = Error.Internal("Search.Failed", "Search service unavailable");

        _searchService.SearchAsync(query, workspaceId, 10, Arg.Any<CancellationToken>())
            .Returns(Result<List<KeywordSearchResult>>.Failure(error));

        // Act
        var result = await _plugin.KeywordSearchAsync(query, workspaceId.ToString(), 10);

        // Assert
        result.Should().Contain("failed");
        result.Should().Contain("Search service unavailable");
    }

    [Fact]
    public async Task KeywordSearchAsync_WithInvalidWorkspaceId_ShouldReturnErrorMessage()
    {
        // Arrange
        var query = "test query";

        // Act
        var result = await _plugin.KeywordSearchAsync(query, "invalid-guid", 10);

        // Assert
        result.Should().Contain("Invalid workspace ID");
    }

    [Fact]
    public async Task KeywordSearchAsync_WithEmptyQuery_ShouldReturnErrorMessage()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();

        // Act
        var result = await _plugin.KeywordSearchAsync("", workspaceId.ToString(), 10);

        // Assert
        result.Should().Contain("Query cannot be empty");
    }
}
