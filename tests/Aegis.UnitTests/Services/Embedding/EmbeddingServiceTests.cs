using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.Embedding;
using FluentAssertions;

namespace Aegis.UnitTests.Services.Embedding;

public class EmbeddingServiceTests
{
    [Fact]
    public async Task GenerateEmbeddingAsync_WithValidText_ShouldReturnEmbedding()
    {
        // Arrange
        var embeddingService = new InMemoryEmbeddingService(dimension: 384);
        var text = "This is a test sentence for embedding.";

        // Act
        var result = await embeddingService.GenerateEmbeddingAsync(text);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Length.Should().Be(384);
        result.Value.Should().AllSatisfy(v => v.Should().BeInRange(-1f, 1f));
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WithEmptyText_ShouldReturnFailure()
    {
        // Arrange
        var embeddingService = new InMemoryEmbeddingService(dimension: 384);
        var text = "";

        // Act
        var result = await embeddingService.GenerateEmbeddingAsync(text);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Contain("EmptyText");
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_WithMultipleTexts_ShouldReturnMultipleEmbeddings()
    {
        // Arrange
        var embeddingService = new InMemoryEmbeddingService(dimension: 384);
        var texts = new[]
        {
            "First sentence.",
            "Second sentence.",
            "Third sentence."
        };

        // Act
        var result = await embeddingService.GenerateEmbeddingsAsync(texts);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);
        result.Value.Should().AllSatisfy(embedding =>
        {
            embedding.Length.Should().Be(384);
            embedding.Should().AllSatisfy(v => v.Should().BeInRange(-1f, 1f));
        });
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_WithEmptyList_ShouldReturnEmptyList()
    {
        // Arrange
        var embeddingService = new InMemoryEmbeddingService(dimension: 384);
        var texts = Array.Empty<string>();

        // Act
        var result = await embeddingService.GenerateEmbeddingsAsync(texts);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_SameTextShouldProduceSimilarEmbeddings()
    {
        // Arrange
        var embeddingService = new InMemoryEmbeddingService(dimension: 384);
        var text = "This is a test sentence.";

        // Act
        var result1 = await embeddingService.GenerateEmbeddingAsync(text);
        var result2 = await embeddingService.GenerateEmbeddingAsync(text);

        // Assert
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();

        // Calculate cosine similarity
        var similarity = CosineSimilarity(result1.Value, result2.Value);
        similarity.Should().BeGreaterThan(0.99f); // Should be very similar (or identical)
    }

    [Fact]
    public void EmbeddingDimension_ShouldReturnCorrectDimension()
    {
        // Arrange
        var embeddingService = new InMemoryEmbeddingService(dimension: 768);

        // Act & Assert
        embeddingService.EmbeddingDimension.Should().Be(768);
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException("Vectors must have the same length");

        float dotProduct = 0;
        float magnitudeA = 0;
        float magnitudeB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }

        magnitudeA = (float)Math.Sqrt(magnitudeA);
        magnitudeB = (float)Math.Sqrt(magnitudeB);

        if (magnitudeA == 0 || magnitudeB == 0)
            return 0;

        return dotProduct / (magnitudeA * magnitudeB);
    }
}
