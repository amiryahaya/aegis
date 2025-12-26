using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Service for reranking retrieved documents based on relevance to a query
/// </summary>
public interface IRerankerService
{
    /// <summary>
    /// Rerank a list of documents based on their relevance to a query
    /// </summary>
    /// <param name="query">The search query</param>
    /// <param name="documents">List of documents to rerank</param>
    /// <param name="topK">Number of top results to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Reranked documents with relevance scores</returns>
    Task<Result<IReadOnlyList<RankedDocument>>> RerankAsync(
        string query,
        IReadOnlyList<string> documents,
        int topK = 10,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a document with its relevance score
/// </summary>
public record RankedDocument(
    int Index,           // Original index in the input list
    string Content,      // Document content
    double RelevanceScore); // Relevance score (higher is more relevant)
