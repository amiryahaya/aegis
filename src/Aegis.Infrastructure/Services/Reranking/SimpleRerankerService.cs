using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Reranking;

/// <summary>
/// Simple reranker service using basic text similarity (for fallback/testing)
/// </summary>
public class SimpleRerankerService : IRerankerService
{
    private readonly ILogger<SimpleRerankerService> _logger;

    public SimpleRerankerService(ILogger<SimpleRerankerService> logger)
    {
        _logger = logger;
    }

    public Task<Result<IReadOnlyList<RankedDocument>>> RerankAsync(
        string query,
        IReadOnlyList<string> documents,
        int topK = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult(Result<IReadOnlyList<RankedDocument>>.Failure(
                Error.Validation("Reranker.EmptyQuery", "Query cannot be empty")));
        }

        if (documents == null || documents.Count == 0)
        {
            return Task.FromResult(Result<IReadOnlyList<RankedDocument>>.Success(
                Array.Empty<RankedDocument>() as IReadOnlyList<RankedDocument>));
        }

        try
        {
            var queryTerms = GetTerms(query.ToLowerInvariant());

            // Score each document based on term overlap
            var scoredDocs = documents
                .Select((doc, index) => new
                {
                    Index = index,
                    Content = doc,
                    Score = CalculateRelevanceScore(queryTerms, GetTerms(doc.ToLowerInvariant()))
                })
                .OrderByDescending(x => x.Score)
                .Take(topK)
                .Select(x => new RankedDocument(x.Index, x.Content, x.Score))
                .ToList();

            _logger.LogInformation("Reranked {Count} documents using simple scorer, returned top {TopK}",
                documents.Count, scoredDocs.Count);

            return Task.FromResult(Result<IReadOnlyList<RankedDocument>>.Success(
                scoredDocs as IReadOnlyList<RankedDocument>));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during simple reranking");
            return Task.FromResult(Result<IReadOnlyList<RankedDocument>>.Failure(
                Error.Internal("Reranker.Error", ex.Message)));
        }
    }

    private HashSet<string> GetTerms(string text)
    {
        // Simple tokenization - split on whitespace and common punctuation
        var terms = text.Split(new[] { ' ', '.', ',', '!', '?', ';', ':', '\n', '\r', '\t' },
            StringSplitOptions.RemoveEmptyEntries);

        return new HashSet<string>(terms.Where(t => t.Length > 2)); // Filter out very short terms
    }

    private double CalculateRelevanceScore(HashSet<string> queryTerms, HashSet<string> docTerms)
    {
        if (queryTerms.Count == 0 || docTerms.Count == 0)
            return 0.0;

        // Calculate Jaccard similarity with term frequency weighting
        var intersection = queryTerms.Intersect(docTerms).Count();
        var union = queryTerms.Union(docTerms).Count();

        var jaccardSimilarity = union > 0 ? (double)intersection / union : 0.0;

        // Boost score if document contains exact query phrase
        var docText = string.Join(" ", docTerms);
        var queryText = string.Join(" ", queryTerms);
        var exactMatchBoost = docText.Contains(queryText) ? 0.2 : 0.0;

        return Math.Min(1.0, jaccardSimilarity + exactMatchBoost);
    }
}
