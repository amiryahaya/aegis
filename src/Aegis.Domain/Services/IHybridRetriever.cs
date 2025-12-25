using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Interface for hybrid retrieval combining semantic (vector) and keyword (BM25) search
/// using Reciprocal Rank Fusion (RRF)
/// </summary>
public interface IHybridRetriever
{
    /// <summary>
    /// Retrieves documents using hybrid search with RRF
    /// </summary>
    /// <param name="collectionName">Name of the collection/index</param>
    /// <param name="query">The search query</param>
    /// <param name="limit">Maximum number of results</param>
    /// <param name="scoreThreshold">Minimum RRF score threshold</param>
    /// <param name="filter">Optional metadata filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<Result<List<HybridSearchResult>>> RetrieveAsync(
        string collectionName,
        string query,
        int limit = 10,
        float? scoreThreshold = null,
        Dictionary<string, string>? filter = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a hybrid search result
/// </summary>
public record HybridSearchResult
{
    /// <summary>
    /// The document ID
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Combined RRF score (higher is more relevant)
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

    /// <summary>
    /// Vector similarity score (if available)
    /// </summary>
    public float? VectorScore { get; init; }

    /// <summary>
    /// BM25 score (if available)
    /// </summary>
    public float? BM25Score { get; init; }
}
