using Aegis.Api.Features.Agents.Plugins;
using Aegis.Domain.Common;
using Aegis.Domain.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Aegis.UnitTests.Features.Agents.Plugins;

public class VectorSearchPluginTests
{
    private readonly ISemanticSearchService _searchService;
    private readonly ILogger<VectorSearchPlugin> _logger;
    private readonly VectorSearchPlugin _plugin;

    public VectorSearchPluginTests()
    {
        _searchService = Substitute.For<ISemanticSearchService>();
        _logger = Substitute.For<ILogger<VectorSearchPlugin>>();
        _plugin = new VectorSearchPlugin(_searchService, _logger);
    }

    [Fact]
    public async Task VectorSearchAsync_WithValidQuery_ShouldReturnResults()
    {
        // Arrange
        var query = "test query about intelligence reports";
        var workspaceId = Guid.NewGuid();
        var expectedResults = new List<SemanticSearchResult>
        {
            new SemanticSearchResult
            {
                DocumentId = Guid.NewGuid(),
                Title = "Document 1",
                Content = "This is the content of document 1 about intelligence",
                Score = 0.95f
            },
            new SemanticSearchResult
            {
                DocumentId = Guid.NewGuid(),
                Title = "Document 2",
                Content = "This is the content of document 2 about reports",
                Score = 0.87f
            }
        };

        _searchService.SearchAsync(query, workspaceId, 10, Arg.Any<CancellationToken>())
            .Returns(Result<List<SemanticSearchResult>>.Success(expectedResults));

        // Act
        var result = await _plugin.VectorSearchAsync(query, workspaceId.ToString(), 10);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Document 1");
        result.Should().Contain("Document 2");
        result.Should().Contain("intelligence");
        result.Should().Contain("0.95");
    }

    [Fact]
    public async Task VectorSearchAsync_WhenSearchFails_ShouldReturnErrorMessage()
    {
        // Arrange
        var query = "test query";
        var workspaceId = Guid.NewGuid();
        var error = Error.NotFound("Documents.NotFound", "No documents found");

        _searchService.SearchAsync(query, workspaceId, 10, Arg.Any<CancellationToken>())
            .Returns(Result<List<SemanticSearchResult>>.Failure(error));

        // Act
        var result = await _plugin.VectorSearchAsync(query, workspaceId.ToString(), 10);

        // Assert
        result.Should().Contain("failed");
        result.Should().Contain("No documents found");
    }

    [Fact]
    public async Task VectorSearchAsync_WithInvalidWorkspaceId_ShouldReturnErrorMessage()
    {
        // Arrange
        var query = "test query";
        var invalidWorkspaceId = "not-a-guid";

        // Act
        var result = await _plugin.VectorSearchAsync(query, invalidWorkspaceId, 10);

        // Assert
        result.Should().Contain("Invalid workspace ID");
    }

    [Fact]
    public async Task VectorSearchAsync_WithEmptyQuery_ShouldReturnErrorMessage()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();

        // Act
        var result = await _plugin.VectorSearchAsync("", workspaceId.ToString(), 10);

        // Assert
        result.Should().Contain("Query cannot be empty");
    }

    [Fact]
    public async Task VectorSearchAsync_ShouldLogInformation()
    {
        // Arrange
        var query = "test query";
        var workspaceId = Guid.NewGuid();
        _searchService.SearchAsync(query, workspaceId, 10, Arg.Any<CancellationToken>())
            .Returns(Result<List<SemanticSearchResult>>.Success(new List<SemanticSearchResult>()));

        // Act
        await _plugin.VectorSearchAsync(query, workspaceId.ToString(), 10);

        // Assert
        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Vector search")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }
}
