using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.BM25;
using FluentAssertions;

namespace Aegis.UnitTests.Services.BM25;

public class BM25IndexerTests
{
    private const string TestIndex = "test_index";

    [Fact]
    public async Task CreateIndexAsync_WithValidParameters_ShouldSucceed()
    {
        // Arrange
        var indexer = new InMemoryBM25Indexer();

        // Act
        var result = await indexer.CreateIndexAsync(TestIndex);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task IndexExistsAsync_AfterCreation_ShouldReturnTrue()
    {
        // Arrange
        var indexer = new InMemoryBM25Indexer();
        await indexer.CreateIndexAsync(TestIndex);

        // Act
        var result = await indexer.IndexExistsAsync(TestIndex);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task IndexExistsAsync_WithNonExistentIndex_ShouldReturnFalse()
    {
        // Arrange
        var indexer = new InMemoryBM25Indexer();

        // Act
        var result = await indexer.IndexExistsAsync("nonexistent");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
    }

    [Fact]
    public async Task IndexDocumentAsync_WithValidDocument_ShouldSucceed()
    {
        // Arrange
        var indexer = new InMemoryBM25Indexer();
        await indexer.CreateIndexAsync(TestIndex);

        var document = new BM25Document
        {
            Id = Guid.NewGuid(),
            Text = "This is a test document about machine learning and artificial intelligence.",
            Metadata = new Dictionary<string, string>
            {
                ["source"] = "test.pdf",
                ["page"] = "1"
            }
        };

        // Act
        var result = await indexer.IndexDocumentAsync(TestIndex, document);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task IndexDocumentAsync_WithNonExistentIndex_ShouldReturnFailure()
    {
        // Arrange
        var indexer = new InMemoryBM25Indexer();

        var document = new BM25Document
        {
            Id = Guid.NewGuid(),
            Text = "Test document",
            Metadata = new Dictionary<string, string>()
        };

        // Act
        var result = await indexer.IndexDocumentAsync("nonexistent", document);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Contain("IndexNotFound");
    }

    [Fact]
    public async Task IndexBatchAsync_WithMultipleDocuments_ShouldSucceed()
    {
        // Arrange
        var indexer = new InMemoryBM25Indexer();
        await indexer.CreateIndexAsync(TestIndex);

        var documents = Enumerable.Range(0, 5).Select(i => new BM25Document
        {
            Id = Guid.NewGuid(),
            Text = $"Document {i} about various topics including technology and science.",
            Metadata = new Dictionary<string, string> { ["index"] = i.ToString() }
        }).ToList();

        // Act
        var result = await indexer.IndexBatchAsync(TestIndex, documents);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task SearchAsync_WithMatchingQuery_ShouldReturnResults()
    {
        // Arrange
        var indexer = new InMemoryBM25Indexer();
        await indexer.CreateIndexAsync(TestIndex);

        var doc1 = new BM25Document
        {
            Id = Guid.NewGuid(),
            Text = "Machine learning is a subset of artificial intelligence.",
            Metadata = new Dictionary<string, string> { ["type"] = "ml" }
        };

        var doc2 = new BM25Document
        {
            Id = Guid.NewGuid(),
            Text = "Deep learning is a type of machine learning.",
            Metadata = new Dictionary<string, string> { ["type"] = "dl" }
        };

        var doc3 = new BM25Document
        {
            Id = Guid.NewGuid(),
            Text = "Cooking recipes and food preparation techniques.",
            Metadata = new Dictionary<string, string> { ["type"] = "food" }
        };

        await indexer.IndexDocumentAsync(TestIndex, doc1);
        await indexer.IndexDocumentAsync(TestIndex, doc2);
        await indexer.IndexDocumentAsync(TestIndex, doc3);

        // Act - Search for machine learning
        var result = await indexer.SearchAsync(TestIndex, "machine learning", limit: 5);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        result.Value.Should().Contain(r => r.Id == doc1.Id || r.Id == doc2.Id);
        result.Value.Should().NotContain(r => r.Id == doc3.Id); // Food doc should not match
        result.Value[0].Score.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SearchAsync_WithScoreThreshold_ShouldFilterResults()
    {
        // Arrange
        var indexer = new InMemoryBM25Indexer();
        await indexer.CreateIndexAsync(TestIndex);

        var exactMatch = new BM25Document
        {
            Id = Guid.NewGuid(),
            Text = "artificial intelligence machine learning deep learning",
            Metadata = new Dictionary<string, string> { ["type"] = "exact" }
        };

        var partialMatch = new BM25Document
        {
            Id = Guid.NewGuid(),
            Text = "machine is a device used for various purposes",
            Metadata = new Dictionary<string, string> { ["type"] = "partial" }
        };

        await indexer.IndexDocumentAsync(TestIndex, exactMatch);
        await indexer.IndexDocumentAsync(TestIndex, partialMatch);

        // Act - Search with high threshold
        var result = await indexer.SearchAsync(
            TestIndex,
            "machine learning artificial intelligence",
            limit: 10,
            scoreThreshold: 2.0f); // BM25 scores typically range from 0 to ~10+

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().AllSatisfy(r => r.Score.Should().BeGreaterOrEqualTo(2.0f));
        result.Value.Should().Contain(r => r.Id == exactMatch.Id);
    }

    [Fact]
    public async Task SearchAsync_WithMetadataFilter_ShouldReturnFilteredResults()
    {
        // Arrange
        var indexer = new InMemoryBM25Indexer();
        await indexer.CreateIndexAsync(TestIndex);

        var targetDoc = new BM25Document
        {
            Id = Guid.NewGuid(),
            Text = "Machine learning algorithms for classification",
            Metadata = new Dictionary<string, string> { ["source"] = "target.pdf" }
        };

        var otherDoc = new BM25Document
        {
            Id = Guid.NewGuid(),
            Text = "Machine learning algorithms for regression",
            Metadata = new Dictionary<string, string> { ["source"] = "other.pdf" }
        };

        await indexer.IndexDocumentAsync(TestIndex, targetDoc);
        await indexer.IndexDocumentAsync(TestIndex, otherDoc);

        // Act
        var result = await indexer.SearchAsync(
            TestIndex,
            "machine learning algorithms",
            limit: 10,
            filter: new Dictionary<string, string> { ["source"] = "target.pdf" });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().AllSatisfy(r => r.Metadata["source"].Should().Be("target.pdf"));
        result.Value.Should().Contain(r => r.Id == targetDoc.Id);
        result.Value.Should().NotContain(r => r.Id == otherDoc.Id);
    }

    [Fact]
    public async Task DeleteAsync_WithValidIds_ShouldRemoveDocuments()
    {
        // Arrange
        var indexer = new InMemoryBM25Indexer();
        await indexer.CreateIndexAsync(TestIndex);

        var docToDelete = new BM25Document
        {
            Id = Guid.NewGuid(),
            Text = "Document to be deleted",
            Metadata = new Dictionary<string, string>()
        };

        var docToKeep = new BM25Document
        {
            Id = Guid.NewGuid(),
            Text = "Document to keep",
            Metadata = new Dictionary<string, string>()
        };

        await indexer.IndexDocumentAsync(TestIndex, docToDelete);
        await indexer.IndexDocumentAsync(TestIndex, docToKeep);

        // Act
        var deleteResult = await indexer.DeleteAsync(TestIndex, new[] { docToDelete.Id });

        // Assert
        deleteResult.IsSuccess.Should().BeTrue();

        // Verify deletion
        var searchResult = await indexer.SearchAsync(TestIndex, "document", limit: 10);
        searchResult.Value.Should().NotContain(r => r.Id == docToDelete.Id);
        searchResult.Value.Should().Contain(r => r.Id == docToKeep.Id);
    }

    [Fact]
    public async Task DeleteIndexAsync_WithExistingIndex_ShouldSucceed()
    {
        // Arrange
        var indexer = new InMemoryBM25Indexer();
        await indexer.CreateIndexAsync(TestIndex);

        // Act
        var result = await indexer.DeleteIndexAsync(TestIndex);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var existsResult = await indexer.IndexExistsAsync(TestIndex);
        existsResult.Value.Should().BeFalse();
    }

    [Fact]
    public async Task SearchAsync_ShouldRankByRelevance()
    {
        // Arrange
        var indexer = new InMemoryBM25Indexer();
        await indexer.CreateIndexAsync(TestIndex);

        var highRelevance = new BM25Document
        {
            Id = Guid.NewGuid(),
            Text = "quantum computing quantum algorithms quantum physics",
            Metadata = new Dictionary<string, string>()
        };

        var mediumRelevance = new BM25Document
        {
            Id = Guid.NewGuid(),
            Text = "quantum computing is an emerging field",
            Metadata = new Dictionary<string, string>()
        };

        var lowRelevance = new BM25Document
        {
            Id = Guid.NewGuid(),
            Text = "computing systems and hardware",
            Metadata = new Dictionary<string, string>()
        };

        await indexer.IndexDocumentAsync(TestIndex, lowRelevance);
        await indexer.IndexDocumentAsync(TestIndex, mediumRelevance);
        await indexer.IndexDocumentAsync(TestIndex, highRelevance);

        // Act
        var result = await indexer.SearchAsync(TestIndex, "quantum computing", limit: 3);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCountGreaterOrEqualTo(2);

        // High relevance should be ranked higher than medium relevance
        var highRank = result.Value.FindIndex(r => r.Id == highRelevance.Id);
        var mediumRank = result.Value.FindIndex(r => r.Id == mediumRelevance.Id);

        highRank.Should().BeLessThan(mediumRank);
        result.Value[0].Score.Should().BeGreaterThan(result.Value[1].Score);
    }
}
