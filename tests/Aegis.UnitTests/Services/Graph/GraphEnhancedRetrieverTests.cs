using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.Graph;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Aegis.UnitTests.Services.Graph;

public class GraphEnhancedRetrieverTests
{
    private readonly Mock<IHybridRetriever> _mockHybridRetriever;
    private readonly Mock<INERService> _mockNerService;
    private readonly Mock<IGraphQueryService> _mockGraphQueryService;
    private readonly GraphEnhancedRetriever _retriever;

    public GraphEnhancedRetrieverTests()
    {
        _mockHybridRetriever = new Mock<IHybridRetriever>();
        _mockNerService = new Mock<INERService>();
        _mockGraphQueryService = new Mock<IGraphQueryService>();

        _retriever = new GraphEnhancedRetriever(
            _mockHybridRetriever.Object,
            _mockNerService.Object,
            _mockGraphQueryService.Object,
            NullLogger<GraphEnhancedRetriever>.Instance);
    }

    [Fact]
    public async Task RetrieveAsync_WithValidQuery_ShouldReturnEnhancedResults()
    {
        // Arrange
        var query = "APT28 uses WannaCry malware";
        var hybridResults = new List<HybridSearchResult>
        {
            new() { Id = Guid.NewGuid(), Score = 0.8f, Text = "APT28 is a threat actor", Metadata = new() }
        };

        var entities = new List<NamedEntity>
        {
            new() { Text = "APT28", Type = NEREntityType.Organization, Confidence = 0.95, StartPosition = 0, EndPosition = 5 }
        };

        _mockHybridRetriever
            .Setup(x => x.RetrieveAsync(It.IsAny<string>(), query, 10, null, null, default))
            .ReturnsAsync(Result<List<HybridSearchResult>>.Success(hybridResults));

        _mockNerService
            .Setup(x => x.ExtractEntitiesAsync(query, default))
            .ReturnsAsync(Result<IReadOnlyList<NamedEntity>>.Success(entities));

        _mockNerService
            .Setup(x => x.ExtractEntitiesAsync(hybridResults[0].Text, default))
            .ReturnsAsync(Result<IReadOnlyList<NamedEntity>>.Success(entities));

        _mockGraphQueryService
            .Setup(x => x.FindSimilarEntitiesAsync(It.IsAny<string>(), 5, default))
            .ReturnsAsync(Result<IReadOnlyList<SimilarEntity>>.Success(new List<SimilarEntity>().AsReadOnly()));

        // Act
        var result = await _retriever.RetrieveAsync("test-collection", query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
    }

    [Fact]
    public async Task RetrieveAsync_WithGraphExpansionEnabled_ShouldBoostRelatedChunks()
    {
        // Arrange
        var query = "APT28 malware";
        var chunkId = Guid.NewGuid();
        var hybridResults = new List<HybridSearchResult>
        {
            new() { Id = chunkId, Score = 0.5f, Text = "APT28 uses sophisticated malware", Metadata = new() }
        };

        var queryEntities = new List<NamedEntity>
        {
            new() { Text = "APT28", Type = NEREntityType.Organization, Confidence = 0.95, StartPosition = 0, EndPosition = 5 }
        };

        // Entities extracted from the chunk text should include "malware"
        var chunkEntities = new List<NamedEntity>
        {
            new() { Text = "APT28", Type = NEREntityType.Organization, Confidence = 0.95, StartPosition = 0, EndPosition = 5 },
            new() { Text = "malware", Type = NEREntityType.Other, Confidence = 0.9, StartPosition = 28, EndPosition = 35 }
        };

        var relatedEntities = new List<SimilarEntity>
        {
            new() { Entity = new GraphEntity { Id = "entity-malware", Name = "malware", Type = EntityType.Malware }, SimilarityScore = 0.9 }
        };

        _mockHybridRetriever
            .Setup(x => x.RetrieveAsync(It.IsAny<string>(), query, 10, null, null, default))
            .ReturnsAsync(Result<List<HybridSearchResult>>.Success(hybridResults));

        _mockNerService
            .Setup(x => x.ExtractEntitiesAsync(query, default))
            .ReturnsAsync(Result<IReadOnlyList<NamedEntity>>.Success(queryEntities));

        _mockNerService
            .Setup(x => x.ExtractEntitiesAsync(hybridResults[0].Text, default))
            .ReturnsAsync(Result<IReadOnlyList<NamedEntity>>.Success(chunkEntities));

        _mockGraphQueryService
            .Setup(x => x.FindSimilarEntitiesAsync(It.IsAny<string>(), 5, default))
            .ReturnsAsync(Result<IReadOnlyList<SimilarEntity>>.Success(relatedEntities.AsReadOnly()));

        // Act
        var result = await _retriever.RetrieveAsync("test-collection", query, useGraphExpansion: true);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        result.Value.First().GraphBoost.Should().BeGreaterThan(0);
        result.Value.First().FinalScore.Should().BeGreaterThan(hybridResults[0].Score);
    }

    [Fact]
    public async Task RetrieveAsync_WithGraphExpansionDisabled_ShouldNotBoost()
    {
        // Arrange
        var query = "APT28";
        var originalScore = 0.8f;
        var hybridResults = new List<HybridSearchResult>
        {
            new() { Id = Guid.NewGuid(), Score = originalScore, Text = "APT28 is active", Metadata = new() }
        };

        _mockHybridRetriever
            .Setup(x => x.RetrieveAsync(It.IsAny<string>(), query, 10, null, null, default))
            .ReturnsAsync(Result<List<HybridSearchResult>>.Success(hybridResults));

        _mockNerService
            .Setup(x => x.ExtractEntitiesAsync(query, default))
            .ReturnsAsync(Result<IReadOnlyList<NamedEntity>>.Success(new List<NamedEntity>()));

        // Act
        var result = await _retriever.RetrieveAsync("test-collection", query, useGraphExpansion: false);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.First().GraphBoost.Should().Be(0);
        result.Value.First().FinalScore.Should().Be(originalScore);
    }

    [Fact]
    public async Task RetrieveAsync_WithEmptyQuery_ShouldReturnFailure()
    {
        // Act
        var result = await _retriever.RetrieveAsync("test-collection", "");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Contain("EmptyQuery");
    }

    [Fact]
    public async Task RetrieveAsync_WhenHybridRetrieverFails_ShouldReturnFailure()
    {
        // Arrange
        _mockHybridRetriever
            .Setup(x => x.RetrieveAsync(It.IsAny<string>(), It.IsAny<string>(), 10, null, null, default))
            .ReturnsAsync(Result<List<HybridSearchResult>>.Failure(
                Error.NotFound("Test.Error", "Test error")));

        // Act
        var result = await _retriever.RetrieveAsync("test-collection", "test query");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task RetrieveAsync_WithScoreThreshold_ShouldFilterResults()
    {
        // Arrange
        var hybridResults = new List<HybridSearchResult>
        {
            new() { Id = Guid.NewGuid(), Score = 0.9f, Text = "High score", Metadata = new() },
            new() { Id = Guid.NewGuid(), Score = 0.3f, Text = "Low score", Metadata = new() }
        };

        _mockHybridRetriever
            .Setup(x => x.RetrieveAsync(It.IsAny<string>(), It.IsAny<string>(), 10, 0.5f, null, default))
            .ReturnsAsync(Result<List<HybridSearchResult>>.Success(
                hybridResults.Where(r => r.Score >= 0.5f).ToList()));

        _mockNerService
            .Setup(x => x.ExtractEntitiesAsync(It.IsAny<string>(), default))
            .ReturnsAsync(Result<IReadOnlyList<NamedEntity>>.Success(new List<NamedEntity>()));

        // Act
        var result = await _retriever.RetrieveAsync("test-collection", "test query", scoreThreshold: 0.5f);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value.All(r => r.Score >= 0.5f).Should().BeTrue();
    }
}
