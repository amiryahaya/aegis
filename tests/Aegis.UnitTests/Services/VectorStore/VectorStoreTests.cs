using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.VectorStore;
using FluentAssertions;

namespace Aegis.UnitTests.Services.VectorStore;

public class VectorStoreTests
{
    private const string TestCollection = "test_collection";
    private const int VectorDimension = 384;

    [Fact]
    public async Task CreateCollectionAsync_WithValidParameters_ShouldSucceed()
    {
        // Arrange
        var vectorStore = new InMemoryVectorStore();

        // Act
        var result = await vectorStore.CreateCollectionAsync(TestCollection, VectorDimension);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CollectionExistsAsync_AfterCreation_ShouldReturnTrue()
    {
        // Arrange
        var vectorStore = new InMemoryVectorStore();
        await vectorStore.CreateCollectionAsync(TestCollection, VectorDimension);

        // Act
        var result = await vectorStore.CollectionExistsAsync(TestCollection);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task CollectionExistsAsync_WithNonExistentCollection_ShouldReturnFalse()
    {
        // Arrange
        var vectorStore = new InMemoryVectorStore();

        // Act
        var result = await vectorStore.CollectionExistsAsync("nonexistent");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
    }

    [Fact]
    public async Task UpsertAsync_WithValidPoint_ShouldSucceed()
    {
        // Arrange
        var vectorStore = new InMemoryVectorStore();
        await vectorStore.CreateCollectionAsync(TestCollection, VectorDimension);

        var point = new VectorPoint
        {
            Id = Guid.NewGuid(),
            Vector = GenerateRandomVector(VectorDimension),
            Metadata = new Dictionary<string, string>
            {
                ["text"] = "Test document",
                ["source"] = "test.pdf"
            }
        };

        // Act
        var result = await vectorStore.UpsertAsync(TestCollection, point);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UpsertBatchAsync_WithMultiplePoints_ShouldSucceed()
    {
        // Arrange
        var vectorStore = new InMemoryVectorStore();
        await vectorStore.CreateCollectionAsync(TestCollection, VectorDimension);

        var points = Enumerable.Range(0, 5).Select(i => new VectorPoint
        {
            Id = Guid.NewGuid(),
            Vector = GenerateRandomVector(VectorDimension),
            Metadata = new Dictionary<string, string> { ["index"] = i.ToString() }
        }).ToList();

        // Act
        var result = await vectorStore.UpsertBatchAsync(TestCollection, points);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task SearchAsync_WithSimilarVector_ShouldReturnResults()
    {
        // Arrange
        var vectorStore = new InMemoryVectorStore();
        await vectorStore.CreateCollectionAsync(TestCollection, VectorDimension);

        var vector = GenerateRandomVector(VectorDimension);
        var point = new VectorPoint
        {
            Id = Guid.NewGuid(),
            Vector = vector,
            Metadata = new Dictionary<string, string> { ["text"] = "Test" }
        };

        await vectorStore.UpsertAsync(TestCollection, point);

        // Act - Search with the same vector (should have high similarity)
        var result = await vectorStore.SearchAsync(TestCollection, vector, limit: 5);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        result.Value[0].Id.Should().Be(point.Id);
        result.Value[0].Score.Should().BeGreaterThan(0.99f); // Should be very similar
    }

    [Fact]
    public async Task SearchAsync_WithScoreThreshold_ShouldFilterResults()
    {
        // Arrange
        var vectorStore = new InMemoryVectorStore();
        await vectorStore.CreateCollectionAsync(TestCollection, VectorDimension);

        var queryVector = GenerateRandomVector(VectorDimension);

        // Insert the query vector itself (will have score = 1.0)
        var highScoreId = Guid.NewGuid();
        await vectorStore.UpsertAsync(TestCollection, new VectorPoint
        {
            Id = highScoreId,
            Vector = queryVector,
            Metadata = new Dictionary<string, string> { ["type"] = "exact" }
        });

        // Insert random vectors (will have lower scores)
        for (int i = 0; i < 5; i++)
        {
            await vectorStore.UpsertAsync(TestCollection, new VectorPoint
            {
                Id = Guid.NewGuid(),
                Vector = GenerateRandomVector(VectorDimension),
                Metadata = new Dictionary<string, string> { ["type"] = "random" }
            });
        }

        // Act - Search with high threshold (only exact match should pass)
        var result = await vectorStore.SearchAsync(
            TestCollection,
            queryVector,
            limit: 10,
            scoreThreshold: 0.95f);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        result.Value.Should().AllSatisfy(r => r.Score.Should().BeGreaterOrEqualTo(0.95f));
        result.Value.Should().Contain(r => r.Id == highScoreId);
    }

    [Fact]
    public async Task SearchAsync_WithMetadataFilter_ShouldReturnFilteredResults()
    {
        // Arrange
        var vectorStore = new InMemoryVectorStore();
        await vectorStore.CreateCollectionAsync(TestCollection, VectorDimension);

        var targetId = Guid.NewGuid();
        await vectorStore.UpsertAsync(TestCollection, new VectorPoint
        {
            Id = targetId,
            Vector = GenerateRandomVector(VectorDimension),
            Metadata = new Dictionary<string, string> { ["source"] = "target.pdf" }
        });

        await vectorStore.UpsertAsync(TestCollection, new VectorPoint
        {
            Id = Guid.NewGuid(),
            Vector = GenerateRandomVector(VectorDimension),
            Metadata = new Dictionary<string, string> { ["source"] = "other.pdf" }
        });

        // Act
        var result = await vectorStore.SearchAsync(
            TestCollection,
            GenerateRandomVector(VectorDimension),
            limit: 10,
            filter: new Dictionary<string, string> { ["source"] = "target.pdf" });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().AllSatisfy(r => r.Metadata["source"].Should().Be("target.pdf"));
    }

    [Fact]
    public async Task DeleteAsync_WithValidIds_ShouldRemoveVectors()
    {
        // Arrange
        var vectorStore = new InMemoryVectorStore();
        await vectorStore.CreateCollectionAsync(TestCollection, VectorDimension);

        var id = Guid.NewGuid();
        await vectorStore.UpsertAsync(TestCollection, new VectorPoint
        {
            Id = id,
            Vector = GenerateRandomVector(VectorDimension),
            Metadata = new Dictionary<string, string>()
        });

        // Act
        var deleteResult = await vectorStore.DeleteAsync(TestCollection, new[] { id });

        // Assert
        deleteResult.IsSuccess.Should().BeTrue();

        // Verify deletion
        var searchResult = await vectorStore.SearchAsync(
            TestCollection,
            GenerateRandomVector(VectorDimension),
            limit: 10);

        searchResult.Value.Should().NotContain(r => r.Id == id);
    }

    [Fact]
    public async Task DeleteCollectionAsync_WithExistingCollection_ShouldSucceed()
    {
        // Arrange
        var vectorStore = new InMemoryVectorStore();
        await vectorStore.CreateCollectionAsync(TestCollection, VectorDimension);

        // Act
        var result = await vectorStore.DeleteCollectionAsync(TestCollection);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var existsResult = await vectorStore.CollectionExistsAsync(TestCollection);
        existsResult.Value.Should().BeFalse();
    }

    private static float[] GenerateRandomVector(int dimension)
    {
        var random = new Random();
        var vector = new float[dimension];

        for (int i = 0; i < dimension; i++)
        {
            vector[i] = (float)(random.NextDouble() * 2 - 1);
        }

        // Normalize to unit vector
        var magnitude = (float)Math.Sqrt(vector.Sum(x => x * x));
        for (int i = 0; i < dimension; i++)
        {
            vector[i] /= magnitude;
        }

        return vector;
    }
}
