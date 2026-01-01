using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.Caching;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Aegis.UnitTests.Services.Caching;

public class EmbeddingCacheTests
{
    private readonly ILogger<InMemoryEmbeddingCache> _logger;
    private readonly InMemoryEmbeddingCache _sut;

    public EmbeddingCacheTests()
    {
        _logger = Substitute.For<ILogger<InMemoryEmbeddingCache>>();
        _sut = new InMemoryEmbeddingCache(_logger);
    }

    private float[] GenerateTestEmbedding(int seed = 0)
    {
        var embedding = new float[384];
        var random = new Random(seed);
        for (int i = 0; i < 384; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2 - 1);
        }
        return embedding;
    }

    #region Cache Miss

    [Fact]
    public async Task GetAsync_WithNoCache_ReturnsMiss()
    {
        // Act
        var result = await _sut.GetAsync("Hello world", "text-embedding-3-small");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsHit.Should().BeFalse();
        result.Value.Embedding.Should().BeNull();
    }

    #endregion

    #region Cache Hit

    [Fact]
    public async Task GetAsync_AfterSet_ReturnsHit()
    {
        // Arrange
        var text = "This is a test sentence.";
        var model = "text-embedding-3-small";
        var embedding = GenerateTestEmbedding(42);

        await _sut.SetAsync(new EmbeddingCacheEntry
        {
            Text = text,
            Embedding = embedding,
            Model = model
        });

        // Act
        var result = await _sut.GetAsync(text, model);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsHit.Should().BeTrue();
        result.Value.Embedding.Should().BeEquivalentTo(embedding);
        result.Value.Model.Should().Be(model);
        result.Value.Dimension.Should().Be(384);
    }

    [Fact]
    public async Task GetAsync_DifferentModel_ReturnsMiss()
    {
        // Arrange
        var text = "Same text different model";
        await _sut.SetAsync(new EmbeddingCacheEntry
        {
            Text = text,
            Embedding = GenerateTestEmbedding(1),
            Model = "model-a"
        });

        // Act
        var result = await _sut.GetAsync(text, "model-b");

        // Assert
        result.Value.IsHit.Should().BeFalse();
    }

    [Fact]
    public async Task GetAsync_SameTextSameModel_ReturnsHit()
    {
        // Arrange
        var text = "Consistent text";
        var model = "consistent-model";
        var embedding = GenerateTestEmbedding(100);

        await _sut.SetAsync(new EmbeddingCacheEntry
        {
            Text = text,
            Embedding = embedding,
            Model = model
        });

        // Act - Multiple gets should all hit
        var result1 = await _sut.GetAsync(text, model);
        var result2 = await _sut.GetAsync(text, model);
        var result3 = await _sut.GetAsync(text, model);

        // Assert
        result1.Value.IsHit.Should().BeTrue();
        result2.Value.IsHit.Should().BeTrue();
        result3.Value.IsHit.Should().BeTrue();
        result1.Value.Embedding.Should().BeEquivalentTo(result2.Value.Embedding);
    }

    #endregion

    #region Batch Operations

    [Fact]
    public async Task GetBatchAsync_WithAllMisses_ReturnsAllMisses()
    {
        // Arrange
        var texts = new List<string> { "Text 1", "Text 2", "Text 3" };

        // Act
        var result = await _sut.GetBatchAsync(texts, "test-model");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.HitCount.Should().Be(0);
        result.Value.MissCount.Should().Be(3);
        result.Value.MissIndices.Should().BeEquivalentTo(new[] { 0, 1, 2 });
        result.Value.MissedTexts.Should().BeEquivalentTo(texts);
    }

    [Fact]
    public async Task GetBatchAsync_WithPartialHits_ReturnsCorrectResults()
    {
        // Arrange
        var model = "batch-model";
        await _sut.SetAsync(new EmbeddingCacheEntry { Text = "Cached 1", Embedding = GenerateTestEmbedding(1), Model = model });
        await _sut.SetAsync(new EmbeddingCacheEntry { Text = "Cached 3", Embedding = GenerateTestEmbedding(3), Model = model });

        var texts = new List<string> { "Cached 1", "Not cached", "Cached 3" };

        // Act
        var result = await _sut.GetBatchAsync(texts, model);

        // Assert
        result.Value.HitCount.Should().Be(2);
        result.Value.MissCount.Should().Be(1);
        result.Value.MissIndices.Should().Contain(1);
        result.Value.MissedTexts.Should().Contain("Not cached");
        result.Value.Results[0].IsHit.Should().BeTrue();
        result.Value.Results[1].IsHit.Should().BeFalse();
        result.Value.Results[2].IsHit.Should().BeTrue();
    }

    [Fact]
    public async Task SetBatchAsync_StoresAllEntries()
    {
        // Arrange
        var model = "batch-set-model";
        var entries = new List<EmbeddingCacheEntry>
        {
            new EmbeddingCacheEntry { Text = "Batch 1", Embedding = GenerateTestEmbedding(1), Model = model },
            new EmbeddingCacheEntry { Text = "Batch 2", Embedding = GenerateTestEmbedding(2), Model = model },
            new EmbeddingCacheEntry { Text = "Batch 3", Embedding = GenerateTestEmbedding(3), Model = model }
        };

        // Act
        var result = await _sut.SetBatchAsync(entries);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(3);

        // Verify all are cached
        var get1 = await _sut.GetAsync("Batch 1", model);
        var get2 = await _sut.GetAsync("Batch 2", model);
        var get3 = await _sut.GetAsync("Batch 3", model);

        get1.Value.IsHit.Should().BeTrue();
        get2.Value.IsHit.Should().BeTrue();
        get3.Value.IsHit.Should().BeTrue();
    }

    #endregion

    #region Invalidation

    [Fact]
    public async Task InvalidateAsync_ByModel_InvalidatesModelEntries()
    {
        // Arrange
        await _sut.SetAsync(new EmbeddingCacheEntry { Text = "Model A text", Embedding = GenerateTestEmbedding(1), Model = "model-a" });
        await _sut.SetAsync(new EmbeddingCacheEntry { Text = "Model B text", Embedding = GenerateTestEmbedding(2), Model = "model-b" });

        // Act
        var result = await _sut.InvalidateAsync(new EmbeddingInvalidationPattern { Model = "model-a" });

        // Assert
        result.Value.Should().Be(1);

        var getA = await _sut.GetAsync("Model A text", "model-a");
        var getB = await _sut.GetAsync("Model B text", "model-b");

        getA.Value.IsHit.Should().BeFalse();
        getB.Value.IsHit.Should().BeTrue();
    }

    [Fact]
    public async Task InvalidateAsync_ByDocumentId_InvalidatesDocumentEntries()
    {
        // Arrange
        var docId = Guid.NewGuid();
        await _sut.SetAsync(new EmbeddingCacheEntry
        {
            Text = "Doc chunk 1",
            Embedding = GenerateTestEmbedding(1),
            Model = "test",
            DocumentId = docId,
            ChunkIndex = 0
        });
        await _sut.SetAsync(new EmbeddingCacheEntry
        {
            Text = "Doc chunk 2",
            Embedding = GenerateTestEmbedding(2),
            Model = "test",
            DocumentId = docId,
            ChunkIndex = 1
        });

        // Act
        var result = await _sut.InvalidateAsync(new EmbeddingInvalidationPattern
        {
            DocumentIds = new List<Guid> { docId }
        });

        // Assert
        result.Value.Should().Be(2);
    }

    [Fact]
    public async Task InvalidateAsync_All_ClearsCache()
    {
        // Arrange
        for (int i = 0; i < 10; i++)
        {
            await _sut.SetAsync(new EmbeddingCacheEntry
            {
                Text = $"Text {i}",
                Embedding = GenerateTestEmbedding(i),
                Model = "test"
            });
        }

        // Act
        var result = await _sut.InvalidateAsync(new EmbeddingInvalidationPattern { InvalidateAll = true });

        // Assert
        result.Value.Should().Be(10);
    }

    #endregion

    #region Statistics

    [Fact]
    public async Task GetStatisticsAsync_TracksHitsAndMisses()
    {
        // Arrange
        await _sut.SetAsync(new EmbeddingCacheEntry
        {
            Text = "Stats text",
            Embedding = GenerateTestEmbedding(1),
            Model = "stats-model"
        });

        // Generate some hits and misses
        await _sut.GetAsync("Stats text", "stats-model"); // Hit
        await _sut.GetAsync("Stats text", "stats-model"); // Hit
        await _sut.GetAsync("Unknown", "stats-model"); // Miss

        // Act
        var stats = await _sut.GetStatisticsAsync();

        // Assert
        stats.IsSuccess.Should().BeTrue();
        stats.Value.TotalHits.Should().BeGreaterOrEqualTo(2);
        stats.Value.TotalMisses.Should().BeGreaterOrEqualTo(1);
        stats.Value.HitRate.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetStatisticsAsync_TracksEntriesByModel()
    {
        // Arrange
        await _sut.SetAsync(new EmbeddingCacheEntry { Text = "A", Embedding = GenerateTestEmbedding(1), Model = "model-x" });
        await _sut.SetAsync(new EmbeddingCacheEntry { Text = "B", Embedding = GenerateTestEmbedding(2), Model = "model-x" });
        await _sut.SetAsync(new EmbeddingCacheEntry { Text = "C", Embedding = GenerateTestEmbedding(3), Model = "model-y" });

        // Act
        var stats = await _sut.GetStatisticsAsync();

        // Assert
        stats.Value.EntriesByModel.Should().ContainKey("model-x");
        stats.Value.EntriesByModel.Should().ContainKey("model-y");
        stats.Value.EntriesByModel["model-x"].Should().Be(2);
        stats.Value.EntriesByModel["model-y"].Should().Be(1);
    }

    [Fact]
    public async Task GetStatisticsAsync_CalculatesApiCallsSaved()
    {
        // Arrange
        await _sut.SetAsync(new EmbeddingCacheEntry
        {
            Text = "Saved call",
            Embedding = GenerateTestEmbedding(1),
            Model = "test"
        });

        // Simulate cache hits (API calls saved)
        for (int i = 0; i < 5; i++)
        {
            await _sut.GetAsync("Saved call", "test");
        }

        // Act
        var stats = await _sut.GetStatisticsAsync();

        // Assert
        stats.Value.ApiCallsSaved.Should().BeGreaterOrEqualTo(5);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task GetAsync_WithEmptyText_ReturnsFailure()
    {
        // Act
        var result = await _sut.GetAsync("", "test-model");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task GetAsync_WithNullText_ReturnsFailure()
    {
        // Act
        var result = await _sut.GetAsync(null!, "test-model");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SetAsync_WithNullEmbedding_ReturnsFailure()
    {
        // Arrange
        var entry = new EmbeddingCacheEntry
        {
            Text = "Test",
            Embedding = null!,
            Model = "test"
        };

        // Act
        var result = await _sut.SetAsync(entry);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task GetBatchAsync_WithEmptyList_ReturnsEmpty()
    {
        // Act
        var result = await _sut.GetBatchAsync(new List<string>(), "test");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Results.Should().BeEmpty();
    }

    #endregion
}
