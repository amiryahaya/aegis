using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Interface for BM25-based keyword search and indexing
/// </summary>
public interface IBM25Indexer
{
    /// <summary>
    /// Creates a new index
    /// </summary>
    /// <param name="indexName">Name of the index</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<Result> CreateIndexAsync(
        string indexName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an index exists
    /// </summary>
    /// <param name="indexName">Name of the index</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<Result<bool>> IndexExistsAsync(
        string indexName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an index
    /// </summary>
    /// <param name="indexName">Name of the index</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<Result> DeleteIndexAsync(
        string indexName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Indexes a single document
    /// </summary>
    /// <param name="indexName">Name of the index</param>
    /// <param name="document">The document to index</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<Result> IndexDocumentAsync(
        string indexName,
        BM25Document document,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Indexes multiple documents in batch
    /// </summary>
    /// <param name="indexName">Name of the index</param>
    /// <param name="documents">The documents to index</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<Result> IndexBatchAsync(
        string indexName,
        IEnumerable<BM25Document> documents,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for documents using BM25 ranking
    /// </summary>
    /// <param name="indexName">Name of the index</param>
    /// <param name="query">The search query</param>
    /// <param name="limit">Maximum number of results</param>
    /// <param name="scoreThreshold">Minimum BM25 score</param>
    /// <param name="filter">Optional metadata filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<Result<List<BM25SearchResult>>> SearchAsync(
        string indexName,
        string query,
        int limit = 10,
        float? scoreThreshold = null,
        Dictionary<string, string>? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes documents by IDs
    /// </summary>
    /// <param name="indexName">Name of the index</param>
    /// <param name="ids">The IDs of documents to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<Result> DeleteAsync(
        string indexName,
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a document to be indexed with BM25
/// </summary>
public record BM25Document
{
    /// <summary>
    /// Unique identifier for the document
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The text content to be indexed
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Metadata associated with the document
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = new();
}

/// <summary>
/// Represents a BM25 search result
/// </summary>
public record BM25SearchResult
{
    /// <summary>
    /// The document ID
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// BM25 relevance score (higher is more relevant)
    /// </summary>
    public required float Score { get; init; }

    /// <summary>
    /// The text content
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Metadata associated with the document
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = new();
}
