using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.BM25;
using Aegis.Infrastructure.Services.Embedding;
using Aegis.Infrastructure.Services.Retrieval;
using Aegis.Infrastructure.Services.VectorStore;
using FluentAssertions;

namespace Aegis.UnitTests.Services.Retrieval;

public class HybridRetrieverTests
{
    private const string TestCollection = "test_collection";
    private const int VectorDimension = 384;

    [Fact]
    public async Task RetrieveAsync_WithMatchingDocuments_ShouldCombineResults()
    {
        // Arrange
        var vectorStore = new InMemoryVectorStore();
        var bm25Indexer = new InMemoryBM25Indexer();
        var embeddingService = new InMemoryEmbeddingService(VectorDimension);
        var retriever = new HybridRetriever(vectorStore, bm25Indexer, embeddingService);

        await vectorStore.CreateCollectionAsync(TestCollection, VectorDimension);
        await bm25Indexer.CreateIndexAsync(TestCollection);

        // Index documents
        var doc1Id = Guid.NewGuid();
        var doc1Text = "Machine learning is a subset of artificial intelligence";

        var doc2Id = Guid.NewGuid();
        var doc2Text = "Deep learning uses neural networks";

        // Get embeddings
        var embedding1Result = await embeddingService.GenerateEmbeddingAsync(doc1Text);
        var embedding2Result = await embeddingService.GenerateEmbeddingAsync(doc2Text);

        // Index in vector store
        await vectorStore.UpsertAsync(TestCollection, new VectorPoint
        {
            Id = doc1Id,
            Vector = embedding1Result.Value,
            Metadata = new Dictionary<string, string> { ["text"] = doc1Text }
        });

        await vectorStore.UpsertAsync(TestCollection, new VectorPoint
        {
            Id = doc2Id,
            Vector = embedding2Result.Value,
            Metadata = new Dictionary<string, string> { ["text"] = doc2Text }
        });

        // Index in BM25
        await bm25Indexer.IndexDocumentAsync(TestCollection, new BM25Document
        {
            Id = doc1Id,
            Text = doc1Text,
            Metadata = new Dictionary<string, string> { ["text"] = doc1Text }
        });

        await bm25Indexer.IndexDocumentAsync(TestCollection, new BM25Document
        {
            Id = doc2Id,
            Text = doc2Text,
            Metadata = new Dictionary<string, string> { ["text"] = doc2Text }
        });

        // Act
        var result = await retriever.RetrieveAsync(
            TestCollection,
            "machine learning artificial intelligence",
            limit: 5);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        result.Value.Should().Contain(r => r.Id == doc1Id);
    }

    [Fact]
    public async Task RetrieveAsync_ShouldBoostDocumentsAppearingInBothSources()
    {
        // Arrange
        var vectorStore = new InMemoryVectorStore();
        var bm25Indexer = new InMemoryBM25Indexer();
        var embeddingService = new InMemoryEmbeddingService(VectorDimension);
        var retriever = new HybridRetriever(vectorStore, bm25Indexer, embeddingService);

        await vectorStore.CreateCollectionAsync(TestCollection, VectorDimension);
        await bm25Indexer.CreateIndexAsync(TestCollection);

        // Document that appears in both semantic and keyword search
        var bothId = Guid.NewGuid();
        var bothText = "quantum computing algorithms for optimization";

        // Document that only appears in vector search (semantically similar)
        var vectorOnlyId = Guid.NewGuid();
        var vectorOnlyText = "quantum computing technology";

        // Document that only appears in BM25 (keyword match but not semantically similar)
        var bm25OnlyId = Guid.NewGuid();
        var bm25OnlyText = "algorithms for data structures";

        // Get embeddings
        var bothEmbedding = (await embeddingService.GenerateEmbeddingAsync(bothText)).Value;
        var vectorOnlyEmbedding = (await embeddingService.GenerateEmbeddingAsync(vectorOnlyText)).Value;
        var bm25OnlyEmbedding = (await embeddingService.GenerateEmbeddingAsync(bm25OnlyText)).Value;

        // Index all in vector store
        await vectorStore.UpsertAsync(TestCollection, new VectorPoint
        {
            Id = bothId,
            Vector = bothEmbedding,
            Metadata = new Dictionary<string, string> { ["text"] = bothText }
        });

        await vectorStore.UpsertAsync(TestCollection, new VectorPoint
        {
            Id = vectorOnlyId,
            Vector = vectorOnlyEmbedding,
            Metadata = new Dictionary<string, string> { ["text"] = vectorOnlyText }
        });

        await vectorStore.UpsertAsync(TestCollection, new VectorPoint
        {
            Id = bm25OnlyId,
            Vector = bm25OnlyEmbedding,
            Metadata = new Dictionary<string, string> { ["text"] = bm25OnlyText }
        });

        // Index all in BM25
        await bm25Indexer.IndexDocumentAsync(TestCollection, new BM25Document
        {
            Id = bothId,
            Text = bothText,
            Metadata = new Dictionary<string, string> { ["text"] = bothText }
        });

        await bm25Indexer.IndexDocumentAsync(TestCollection, new BM25Document
        {
            Id = vectorOnlyId,
            Text = vectorOnlyText,
            Metadata = new Dictionary<string, string> { ["text"] = vectorOnlyText }
        });

        await bm25Indexer.IndexDocumentAsync(TestCollection, new BM25Document
        {
            Id = bm25OnlyId,
            Text = bm25OnlyText,
            Metadata = new Dictionary<string, string> { ["text"] = bm25OnlyText }
        });

        // Act - Query for "quantum computing algorithms"
        var result = await retriever.RetrieveAsync(
            TestCollection,
            "quantum computing algorithms",
            limit: 3);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();

        // The document appearing in both should have both scores populated
        var bothResult = result.Value.FirstOrDefault(r => r.Id == bothId);
        bothResult.Should().NotBeNull();
        bothResult!.VectorScore.Should().NotBeNull();
        bothResult.BM25Score.Should().NotBeNull();
        bothResult.Score.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task RetrieveAsync_WithMetadataFilter_ShouldFilterResults()
    {
        // Arrange
        var vectorStore = new InMemoryVectorStore();
        var bm25Indexer = new InMemoryBM25Indexer();
        var embeddingService = new InMemoryEmbeddingService(VectorDimension);
        var retriever = new HybridRetriever(vectorStore, bm25Indexer, embeddingService);

        await vectorStore.CreateCollectionAsync(TestCollection, VectorDimension);
        await bm25Indexer.CreateIndexAsync(TestCollection);

        var targetId = Guid.NewGuid();
        var targetText = "Machine learning for classification";

        var otherId = Guid.NewGuid();
        var otherText = "Machine learning for regression";

        var targetEmbedding = (await embeddingService.GenerateEmbeddingAsync(targetText)).Value;
        var otherEmbedding = (await embeddingService.GenerateEmbeddingAsync(otherText)).Value;

        // Index with different sources
        await vectorStore.UpsertAsync(TestCollection, new VectorPoint
        {
            Id = targetId,
            Vector = targetEmbedding,
            Metadata = new Dictionary<string, string>
            {
                ["text"] = targetText,
                ["source"] = "target.pdf"
            }
        });

        await vectorStore.UpsertAsync(TestCollection, new VectorPoint
        {
            Id = otherId,
            Vector = otherEmbedding,
            Metadata = new Dictionary<string, string>
            {
                ["text"] = otherText,
                ["source"] = "other.pdf"
            }
        });

        await bm25Indexer.IndexDocumentAsync(TestCollection, new BM25Document
        {
            Id = targetId,
            Text = targetText,
            Metadata = new Dictionary<string, string>
            {
                ["text"] = targetText,
                ["source"] = "target.pdf"
            }
        });

        await bm25Indexer.IndexDocumentAsync(TestCollection, new BM25Document
        {
            Id = otherId,
            Text = otherText,
            Metadata = new Dictionary<string, string>
            {
                ["text"] = otherText,
                ["source"] = "other.pdf"
            }
        });

        // Act
        var result = await retriever.RetrieveAsync(
            TestCollection,
            "machine learning",
            limit: 10,
            filter: new Dictionary<string, string> { ["source"] = "target.pdf" });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().AllSatisfy(r => r.Metadata["source"].Should().Be("target.pdf"));
        result.Value.Should().Contain(r => r.Id == targetId);
        result.Value.Should().NotContain(r => r.Id == otherId);
    }

    [Fact]
    public async Task RetrieveAsync_WithScoreThreshold_ShouldFilterResults()
    {
        // Arrange
        var vectorStore = new InMemoryVectorStore();
        var bm25Indexer = new InMemoryBM25Indexer();
        var embeddingService = new InMemoryEmbeddingService(VectorDimension);
        var retriever = new HybridRetriever(vectorStore, bm25Indexer, embeddingService);

        await vectorStore.CreateCollectionAsync(TestCollection, VectorDimension);
        await bm25Indexer.CreateIndexAsync(TestCollection);

        var highRelevanceId = Guid.NewGuid();
        var highRelevanceText = "artificial intelligence machine learning deep learning";

        var lowRelevanceId = Guid.NewGuid();
        var lowRelevanceText = "cooking recipes and food";

        var highEmbedding = (await embeddingService.GenerateEmbeddingAsync(highRelevanceText)).Value;
        var lowEmbedding = (await embeddingService.GenerateEmbeddingAsync(lowRelevanceText)).Value;

        await vectorStore.UpsertAsync(TestCollection, new VectorPoint
        {
            Id = highRelevanceId,
            Vector = highEmbedding,
            Metadata = new Dictionary<string, string> { ["text"] = highRelevanceText }
        });

        await vectorStore.UpsertAsync(TestCollection, new VectorPoint
        {
            Id = lowRelevanceId,
            Vector = lowEmbedding,
            Metadata = new Dictionary<string, string> { ["text"] = lowRelevanceText }
        });

        await bm25Indexer.IndexDocumentAsync(TestCollection, new BM25Document
        {
            Id = highRelevanceId,
            Text = highRelevanceText,
            Metadata = new Dictionary<string, string> { ["text"] = highRelevanceText }
        });

        await bm25Indexer.IndexDocumentAsync(TestCollection, new BM25Document
        {
            Id = lowRelevanceId,
            Text = lowRelevanceText,
            Metadata = new Dictionary<string, string> { ["text"] = lowRelevanceText }
        });

        // Act - Use realistic RRF threshold (RRF scores are typically 0.01-0.05)
        var result = await retriever.RetrieveAsync(
            TestCollection,
            "artificial intelligence machine learning",
            limit: 10,
            scoreThreshold: 0.02f);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().AllSatisfy(r => r.Score.Should().BeGreaterOrEqualTo(0.02f));
        result.Value.Should().Contain(r => r.Id == highRelevanceId);
    }

    [Fact]
    public async Task RetrieveAsync_WithNonExistentCollection_ShouldReturnFailure()
    {
        // Arrange
        var vectorStore = new InMemoryVectorStore();
        var bm25Indexer = new InMemoryBM25Indexer();
        var embeddingService = new InMemoryEmbeddingService(VectorDimension);
        var retriever = new HybridRetriever(vectorStore, bm25Indexer, embeddingService);

        // Act
        var result = await retriever.RetrieveAsync("nonexistent", "query");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task RetrieveAsync_WithEmptyQuery_ShouldReturnFailure()
    {
        // Arrange
        var vectorStore = new InMemoryVectorStore();
        var bm25Indexer = new InMemoryBM25Indexer();
        var embeddingService = new InMemoryEmbeddingService(VectorDimension);
        var retriever = new HybridRetriever(vectorStore, bm25Indexer, embeddingService);

        await vectorStore.CreateCollectionAsync(TestCollection, VectorDimension);
        await bm25Indexer.CreateIndexAsync(TestCollection);

        // Act
        var result = await retriever.RetrieveAsync(TestCollection, "");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Contain("EmptyQuery");
    }
}
