using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.Caching;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Aegis.UnitTests.Services.Caching;

public class SemanticCacheTests
{
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<InMemorySemanticCache> _logger;
    private readonly InMemorySemanticCache _sut;

    public SemanticCacheTests()
    {
        _embeddingService = Substitute.For<IEmbeddingService>();
        _logger = Substitute.For<ILogger<InMemorySemanticCache>>();
        _sut = new InMemorySemanticCache(_embeddingService, _logger);

        // Setup default embedding behavior
        _embeddingService.GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var text = callInfo.Arg<string>();
                // Generate deterministic embeddings based on text
                var embedding = GenerateTestEmbedding(text);
                return Task.FromResult(Aegis.Domain.Common.Result<float[]>.Success(embedding));
            });
    }

    private float[] GenerateTestEmbedding(string text)
    {
        // Simple deterministic embedding for testing
        var hash = text.GetHashCode();
        var embedding = new float[384];
        var random = new Random(hash);
        for (int i = 0; i < 384; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2 - 1);
        }
        // Normalize
        var magnitude = (float)Math.Sqrt(embedding.Sum(x => x * x));
        for (int i = 0; i < 384; i++)
        {
            embedding[i] /= magnitude;
        }
        return embedding;
    }

    #region Cache Miss

    [Fact]
    public async Task GetAsync_WithNoCache_ReturnsMiss()
    {
        // Arrange
        var request = new SemanticCacheRequest
        {
            Query = "What is machine learning?"
        };

        // Act
        var result = await _sut.GetAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsHit.Should().BeFalse();
        result.Value.CachedResponse.Should().BeNull();
    }

    #endregion

    #region Cache Hit

    [Fact]
    public async Task GetAsync_AfterSet_ReturnsHit()
    {
        // Arrange
        var query = "What is artificial intelligence?";
        var response = "AI is the simulation of human intelligence by machines.";
        var embedding = GenerateTestEmbedding(query);

        await _sut.SetAsync(new SemanticCacheEntry
        {
            Query = query,
            QueryEmbedding = embedding,
            Response = response
        });

        // Act
        var result = await _sut.GetAsync(new SemanticCacheRequest
        {
            Query = query,
            QueryEmbedding = embedding,
            SimilarityThreshold = 0.95
        });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsHit.Should().BeTrue();
        result.Value.CachedResponse.Should().Be(response);
        result.Value.SimilarityScore.Should().BeGreaterOrEqualTo(0.95);
    }

    [Fact]
    public async Task GetAsync_WithSimilarQuery_ReturnsHit()
    {
        // Arrange - Cache an entry
        var originalQuery = "What is deep learning?";
        var embedding1 = GenerateTestEmbedding(originalQuery);
        await _sut.SetAsync(new SemanticCacheEntry
        {
            Query = originalQuery,
            QueryEmbedding = embedding1,
            Response = "Deep learning is a subset of ML using neural networks."
        });

        // Create a similar embedding (slightly modified)
        var similarEmbedding = embedding1.ToArray();
        similarEmbedding[0] += 0.001f; // Tiny modification

        // Act
        var result = await _sut.GetAsync(new SemanticCacheRequest
        {
            Query = "What's deep learning?",
            QueryEmbedding = similarEmbedding,
            SimilarityThreshold = 0.9
        });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsHit.Should().BeTrue();
    }

    [Fact]
    public async Task GetAsync_WithDifferentQuery_ReturnsMiss()
    {
        // Arrange
        var embedding1 = GenerateTestEmbedding("What is machine learning?");
        await _sut.SetAsync(new SemanticCacheEntry
        {
            Query = "What is machine learning?",
            QueryEmbedding = embedding1,
            Response = "ML is a type of AI."
        });

        var embedding2 = GenerateTestEmbedding("How to cook pasta?");

        // Act
        var result = await _sut.GetAsync(new SemanticCacheRequest
        {
            Query = "How to cook pasta?",
            QueryEmbedding = embedding2,
            SimilarityThreshold = 0.95
        });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsHit.Should().BeFalse();
    }

    #endregion

    #region Workspace Scoping

    [Fact]
    public async Task GetAsync_WithDifferentWorkspace_ReturnsMiss()
    {
        // Arrange
        var workspace1 = Guid.NewGuid();
        var workspace2 = Guid.NewGuid();
        var embedding = GenerateTestEmbedding("What is AI?");

        await _sut.SetAsync(new SemanticCacheEntry
        {
            Query = "What is AI?",
            QueryEmbedding = embedding,
            Response = "AI response",
            WorkspaceId = workspace1
        });

        // Act
        var result = await _sut.GetAsync(new SemanticCacheRequest
        {
            Query = "What is AI?",
            QueryEmbedding = embedding,
            WorkspaceId = workspace2
        });

        // Assert
        result.Value.IsHit.Should().BeFalse();
    }

    [Fact]
    public async Task GetAsync_WithSameWorkspace_ReturnsHit()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var embedding = GenerateTestEmbedding("What is NLP?");

        await _sut.SetAsync(new SemanticCacheEntry
        {
            Query = "What is NLP?",
            QueryEmbedding = embedding,
            Response = "NLP response",
            WorkspaceId = workspaceId
        });

        // Act
        var result = await _sut.GetAsync(new SemanticCacheRequest
        {
            Query = "What is NLP?",
            QueryEmbedding = embedding,
            WorkspaceId = workspaceId
        });

        // Assert
        result.Value.IsHit.Should().BeTrue();
    }

    #endregion

    #region Invalidation

    [Fact]
    public async Task InvalidateAsync_ByWorkspace_InvalidatesWorkspaceEntries()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var embedding = GenerateTestEmbedding("Test query");

        await _sut.SetAsync(new SemanticCacheEntry
        {
            Query = "Test query",
            QueryEmbedding = embedding,
            Response = "Test response",
            WorkspaceId = workspaceId
        });

        // Act
        var invalidated = await _sut.InvalidateAsync(new CacheInvalidationPattern
        {
            WorkspaceId = workspaceId
        });

        // Assert
        invalidated.IsSuccess.Should().BeTrue();
        invalidated.Value.Should().BeGreaterOrEqualTo(1);

        var result = await _sut.GetAsync(new SemanticCacheRequest
        {
            Query = "Test query",
            QueryEmbedding = embedding,
            WorkspaceId = workspaceId
        });
        result.Value.IsHit.Should().BeFalse();
    }

    [Fact]
    public async Task InvalidateAsync_ByTags_InvalidatesTaggedEntries()
    {
        // Arrange
        var embedding = GenerateTestEmbedding("Tagged query");

        await _sut.SetAsync(new SemanticCacheEntry
        {
            Query = "Tagged query",
            QueryEmbedding = embedding,
            Response = "Tagged response",
            Tags = new List<string> { "finance", "report" }
        });

        // Act
        var invalidated = await _sut.InvalidateAsync(new CacheInvalidationPattern
        {
            Tags = new List<string> { "finance" }
        });

        // Assert
        invalidated.Value.Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task InvalidateAsync_All_ClearsAllEntries()
    {
        // Arrange
        for (int i = 0; i < 5; i++)
        {
            var embedding = GenerateTestEmbedding($"Query {i}");
            await _sut.SetAsync(new SemanticCacheEntry
            {
                Query = $"Query {i}",
                QueryEmbedding = embedding,
                Response = $"Response {i}"
            });
        }

        // Act
        var invalidated = await _sut.InvalidateAsync(new CacheInvalidationPattern
        {
            InvalidateAll = true
        });

        // Assert
        invalidated.Value.Should().Be(5);
    }

    #endregion

    #region Statistics

    [Fact]
    public async Task GetStatisticsAsync_ReturnsCorrectStats()
    {
        // Arrange
        var embedding = GenerateTestEmbedding("Stats query");
        await _sut.SetAsync(new SemanticCacheEntry
        {
            Query = "Stats query",
            QueryEmbedding = embedding,
            Response = "Stats response"
        });

        // Record a hit and a miss
        await _sut.GetAsync(new SemanticCacheRequest { Query = "Stats query", QueryEmbedding = embedding });
        await _sut.GetAsync(new SemanticCacheRequest { Query = "Unknown", QueryEmbedding = GenerateTestEmbedding("Unknown") });

        // Act
        var stats = await _sut.GetStatisticsAsync();

        // Assert
        stats.IsSuccess.Should().BeTrue();
        stats.Value.TotalEntries.Should().BeGreaterOrEqualTo(1);
        stats.Value.TotalHits.Should().BeGreaterOrEqualTo(1);
        stats.Value.TotalMisses.Should().BeGreaterOrEqualTo(1);
    }

    #endregion

    #region TTL

    [Fact]
    public async Task SetAsync_WithTtl_StoresTtl()
    {
        // Arrange
        var embedding = GenerateTestEmbedding("TTL query");

        await _sut.SetAsync(new SemanticCacheEntry
        {
            Query = "TTL query",
            QueryEmbedding = embedding,
            Response = "TTL response",
            TtlSeconds = 3600
        });

        // Act
        var result = await _sut.GetAsync(new SemanticCacheRequest
        {
            Query = "TTL query",
            QueryEmbedding = embedding
        });

        // Assert
        result.Value.IsHit.Should().BeTrue();
        result.Value.TtlSeconds.Should().BeGreaterThan(0);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task GetAsync_WithNullQuery_ReturnsFailure()
    {
        // Arrange
        var request = new SemanticCacheRequest
        {
            Query = null!
        };

        // Act
        var result = await _sut.GetAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SetAsync_WithEmptyResponse_Succeeds()
    {
        // Arrange
        var embedding = GenerateTestEmbedding("Empty response query");

        // Act
        var result = await _sut.SetAsync(new SemanticCacheEntry
        {
            Query = "Empty response query",
            QueryEmbedding = embedding,
            Response = ""
        });

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion
}
