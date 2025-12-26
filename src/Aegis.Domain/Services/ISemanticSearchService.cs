using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// High-level semantic search service combining embeddings and vector search
/// </summary>
public interface ISemanticSearchService
{
    /// <summary>
    /// Performs semantic search using natural language query
    /// </summary>
    /// <param name="query">The search query text</param>
    /// <param name="workspaceId">Workspace ID for scoped search</param>
    /// <param name="topK">Maximum number of results</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<Result<List<SemanticSearchResult>>> SearchAsync(
        string query,
        Guid workspaceId,
        int topK = 10,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Semantic search result with document metadata
/// </summary>
public record SemanticSearchResult
{
    public required Guid DocumentId { get; init; }
    public required string Title { get; init; }
    public required string Content { get; init; }
    public required float Score { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = new();
}
