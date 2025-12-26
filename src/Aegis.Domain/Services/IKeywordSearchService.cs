using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Keyword-based search service using BM25 algorithm
/// </summary>
public interface IKeywordSearchService
{
    /// <summary>
    /// Performs keyword-based search using BM25 ranking
    /// </summary>
    /// <param name="query">The search query text</param>
    /// <param name="workspaceId">Workspace ID for scoped search</param>
    /// <param name="topK">Maximum number of results</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<Result<List<KeywordSearchResult>>> SearchAsync(
        string query,
        Guid workspaceId,
        int topK = 10,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Keyword search result with BM25 relevance score
/// </summary>
public record KeywordSearchResult
{
    public required Guid DocumentId { get; init; }
    public required string Title { get; init; }
    public required string Content { get; init; }
    public required float BM25Score { get; init; }
    public required string[] MatchedTerms { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = new();
}
