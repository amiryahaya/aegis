using Aegis.Domain.Common;
using Aegis.Domain.Services;

namespace Aegis.Infrastructure.Services.Retrieval;

/// <summary>
/// Hybrid retriever that combines vector search and BM25 using Reciprocal Rank Fusion (RRF)
/// </summary>
public class HybridRetriever : IHybridRetriever
{
    private readonly IVectorStore _vectorStore;
    private readonly IBM25Indexer _bm25Indexer;
    private readonly IEmbeddingService _embeddingService;

    // RRF constant k (typically 60)
    private const float RrfK = 60f;

    public HybridRetriever(
        IVectorStore vectorStore,
        IBM25Indexer bm25Indexer,
        IEmbeddingService embeddingService)
    {
        _vectorStore = vectorStore;
        _bm25Indexer = bm25Indexer;
        _embeddingService = embeddingService;
    }

    public async Task<Result<List<HybridSearchResult>>> RetrieveAsync(
        string collectionName,
        string query,
        int limit = 10,
        float? scoreThreshold = null,
        Dictionary<string, string>? filter = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Result<List<HybridSearchResult>>.Failure(
                Error.Validation("HybridRetriever.EmptyQuery", "Query cannot be empty"));
        }

        // Generate query embedding
        var embeddingResult = await _embeddingService.GenerateEmbeddingAsync(query, cancellationToken);
        if (embeddingResult.IsFailure)
        {
            return Result<List<HybridSearchResult>>.Failure(embeddingResult.Error!);
        }

        // Perform vector search
        var vectorSearchResult = await _vectorStore.SearchAsync(
            collectionName,
            embeddingResult.Value,
            limit: limit * 2, // Get more results for better fusion
            filter: filter,
            cancellationToken: cancellationToken);

        if (vectorSearchResult.IsFailure)
        {
            return Result<List<HybridSearchResult>>.Failure(vectorSearchResult.Error!);
        }

        // Perform BM25 search
        var bm25SearchResult = await _bm25Indexer.SearchAsync(
            collectionName,
            query,
            limit: limit * 2, // Get more results for better fusion
            filter: filter,
            cancellationToken: cancellationToken);

        if (bm25SearchResult.IsFailure)
        {
            return Result<List<HybridSearchResult>>.Failure(bm25SearchResult.Error!);
        }

        // Combine results using RRF
        var combinedResults = CombineWithRRF(
            vectorSearchResult.Value,
            bm25SearchResult.Value);

        // Apply score threshold if provided
        if (scoreThreshold.HasValue)
        {
            combinedResults = combinedResults
                .Where(r => r.Score >= scoreThreshold.Value)
                .ToList();
        }

        // Sort by RRF score and take top results
        var topResults = combinedResults
            .OrderByDescending(r => r.Score)
            .Take(limit)
            .ToList();

        return Result<List<HybridSearchResult>>.Success(topResults);
    }

    private static List<HybridSearchResult> CombineWithRRF(
        List<VectorSearchResult> vectorResults,
        List<BM25SearchResult> bm25Results)
    {
        // Create dictionaries for rank lookup
        var vectorRanks = vectorResults
            .Select((result, index) => new { result.Id, Rank = index + 1 })
            .ToDictionary(x => x.Id, x => x.Rank);

        var bm25Ranks = bm25Results
            .Select((result, index) => new { result.Id, Rank = index + 1 })
            .ToDictionary(x => x.Id, x => x.Rank);

        // Get all unique document IDs
        var allIds = vectorResults.Select(r => r.Id)
            .Union(bm25Results.Select(r => r.Id))
            .Distinct();

        var combinedResults = new List<HybridSearchResult>();

        foreach (var id in allIds)
        {
            // Calculate RRF score
            float rrfScore = 0;

            if (vectorRanks.TryGetValue(id, out var vectorRank))
            {
                rrfScore += 1.0f / (RrfK + vectorRank);
            }

            if (bm25Ranks.TryGetValue(id, out var bm25Rank))
            {
                rrfScore += 1.0f / (RrfK + bm25Rank);
            }

            // Get the document details (prefer vector result if available)
            var vectorResult = vectorResults.FirstOrDefault(r => r.Id == id);
            var bm25Result = bm25Results.FirstOrDefault(r => r.Id == id);

            string text;
            Dictionary<string, string> metadata;

            if (vectorResult != null)
            {
                text = vectorResult.Metadata.GetValueOrDefault("text", "");
                metadata = new Dictionary<string, string>(vectorResult.Metadata);
            }
            else
            {
                text = bm25Result!.Text;
                metadata = new Dictionary<string, string>(bm25Result.Metadata);
            }

            combinedResults.Add(new HybridSearchResult
            {
                Id = id,
                Score = rrfScore,
                Text = text,
                Metadata = metadata,
                VectorScore = vectorResult?.Score,
                BM25Score = bm25Result?.Score
            });
        }

        return combinedResults;
    }
}
