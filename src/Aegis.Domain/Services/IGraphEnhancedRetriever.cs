using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Retriever enhanced with knowledge graph capabilities
/// </summary>
public interface IGraphEnhancedRetriever
{
    /// <summary>
    /// Retrieves chunks with graph-enhanced ranking
    /// </summary>
    /// <param name="collectionName">Collection to search</param>
    /// <param name="query">Search query</param>
    /// <param name="limit">Maximum results</param>
    /// <param name="scoreThreshold">Minimum score threshold</param>
    /// <param name="useGraphExpansion">Whether to expand query with graph context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Graph-enhanced search results</returns>
    Task<Result<List<GraphEnhancedSearchResult>>> RetrieveAsync(
        string collectionName,
        string query,
        int limit = 10,
        float? scoreThreshold = null,
        bool useGraphExpansion = true,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Search result enhanced with graph information
/// </summary>
public record GraphEnhancedSearchResult : HybridSearchResult
{
    /// <summary>
    /// Entities detected in this chunk
    /// </summary>
    public IReadOnlyList<NamedEntity> Entities { get; init; } = Array.Empty<NamedEntity>();

    /// <summary>
    /// Graph entities related to the query entities
    /// </summary>
    public IReadOnlyList<GraphEntity> RelatedGraphEntities { get; init; } = Array.Empty<GraphEntity>();

    /// <summary>
    /// Graph boost applied to the score
    /// </summary>
    public float GraphBoost { get; init; }

    /// <summary>
    /// Final score including graph boost
    /// </summary>
    public float FinalScore { get; init; }

    /// <summary>
    /// Explanation of why this result was boosted/ranked
    /// </summary>
    public string? RelevanceExplanation { get; init; }
}
