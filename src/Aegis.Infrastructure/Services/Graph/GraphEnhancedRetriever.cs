using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Graph;

/// <summary>
/// Retriever that enhances hybrid search with knowledge graph context
/// </summary>
public class GraphEnhancedRetriever : IGraphEnhancedRetriever
{
    private readonly IHybridRetriever _hybridRetriever;
    private readonly INERService _nerService;
    private readonly IGraphQueryService _graphQueryService;
    private readonly ILogger<GraphEnhancedRetriever> _logger;

    // Boost factor for entities found in graph
    private const float GraphBoostFactor = 0.3f;

    public GraphEnhancedRetriever(
        IHybridRetriever hybridRetriever,
        INERService nerService,
        IGraphQueryService graphQueryService,
        ILogger<GraphEnhancedRetriever> logger)
    {
        _hybridRetriever = hybridRetriever;
        _nerService = nerService;
        _graphQueryService = graphQueryService;
        _logger = logger;
    }

    public async Task<Result<List<GraphEnhancedSearchResult>>> RetrieveAsync(
        string collectionName,
        string query,
        int limit = 10,
        float? scoreThreshold = null,
        bool useGraphExpansion = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Result<List<GraphEnhancedSearchResult>>.Failure(
                Error.Validation("GraphRetriever.EmptyQuery", "Query cannot be empty"));
        }

        try
        {
            // Step 1: Perform hybrid retrieval
            var hybridResult = await _hybridRetriever.RetrieveAsync(
                collectionName,
                query,
                limit,
                scoreThreshold,
                cancellationToken: cancellationToken);

            if (hybridResult.IsFailure)
            {
                return Result<List<GraphEnhancedSearchResult>>.Failure(hybridResult.Error!);
            }

            if (!useGraphExpansion)
            {
                // Return results without graph enhancement
                return Result<List<GraphEnhancedSearchResult>>.Success(
                    hybridResult.Value.Select(r => new GraphEnhancedSearchResult
                    {
                        Id = r.Id,
                        Score = r.Score,
                        Text = r.Text,
                        Metadata = r.Metadata,
                        VectorScore = r.VectorScore,
                        BM25Score = r.BM25Score,
                        Entities = Array.Empty<NamedEntity>(),
                        RelatedGraphEntities = Array.Empty<GraphEntity>(),
                        GraphBoost = 0,
                        FinalScore = r.Score,
                        RelevanceExplanation = null
                    }).ToList());
            }

            // Step 2: Extract entities from query
            var queryEntitiesResult = await _nerService.ExtractEntitiesAsync(query, cancellationToken);
            var queryEntities = queryEntitiesResult.IsSuccess ? queryEntitiesResult.Value : Array.Empty<NamedEntity>();

            // Step 3: Find related entities in graph for query entities
            var relatedGraphEntities = await GetRelatedGraphEntitiesAsync(queryEntities, cancellationToken);

            // Step 4: Enhance each result with graph context
            var enhancedResults = new List<GraphEnhancedSearchResult>();

            foreach (var result in hybridResult.Value)
            {
                var enhancedResult = await EnhanceResultWithGraphAsync(
                    result,
                    queryEntities.ToList(),
                    relatedGraphEntities,
                    cancellationToken);

                enhancedResults.Add(enhancedResult);
            }

            // Step 5: Re-rank by final score
            var rankedResults = enhancedResults
                .OrderByDescending(r => r.FinalScore)
                .ToList();

            return Result<List<GraphEnhancedSearchResult>>.Success(rankedResults);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform graph-enhanced retrieval for query: {Query}", query);
            return Result<List<GraphEnhancedSearchResult>>.Failure(
                Error.Internal("GraphRetriever.Failed", $"Retrieval failed: {ex.Message}"));
        }
    }

    private async Task<List<GraphEntity>> GetRelatedGraphEntitiesAsync(
        IReadOnlyList<NamedEntity> queryEntities,
        CancellationToken cancellationToken)
    {
        var relatedEntities = new List<GraphEntity>();

        foreach (var entity in queryEntities)
        {
            // Generate entity ID similar to EntityIngestionService
            var entityId = GenerateEntityId(entity.Text, entity.Type.ToString());

            try
            {
                var similarResult = await _graphQueryService.FindSimilarEntitiesAsync(
                    entityId,
                    limit: 5,
                    cancellationToken);

                if (similarResult.IsSuccess)
                {
                    relatedEntities.AddRange(similarResult.Value.Select(s => s.Entity));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to find related entities for: {EntityText}", entity.Text);
            }
        }

        return relatedEntities.DistinctBy(e => e.Id).ToList();
    }

    private async Task<GraphEnhancedSearchResult> EnhanceResultWithGraphAsync(
        HybridSearchResult result,
        List<NamedEntity> queryEntities,
        List<GraphEntity> relatedGraphEntities,
        CancellationToken cancellationToken)
    {
        // Extract entities from the chunk text
        var chunkEntitiesResult = await _nerService.ExtractEntitiesAsync(result.Text, cancellationToken);
        var chunkEntities = chunkEntitiesResult.IsSuccess
            ? chunkEntitiesResult.Value.ToList()
            : new List<NamedEntity>();

        // Calculate graph boost based on entity overlap
        var graphBoost = CalculateGraphBoost(chunkEntities, relatedGraphEntities);

        // Generate relevance explanation
        var explanation = GenerateRelevanceExplanation(chunkEntities, relatedGraphEntities, graphBoost);

        // Filter related entities to only those mentioned in the chunk
        var relevantGraphEntities = FilterRelevantGraphEntities(chunkEntities, relatedGraphEntities);

        return new GraphEnhancedSearchResult
        {
            Id = result.Id,
            Score = result.Score,
            Text = result.Text,
            Metadata = result.Metadata,
            VectorScore = result.VectorScore,
            BM25Score = result.BM25Score,
            Entities = chunkEntities.AsReadOnly(),
            RelatedGraphEntities = relevantGraphEntities.AsReadOnly(),
            GraphBoost = graphBoost,
            FinalScore = result.Score + graphBoost,
            RelevanceExplanation = explanation
        };
    }

    private static float CalculateGraphBoost(
        List<NamedEntity> chunkEntities,
        List<GraphEntity> relatedGraphEntities)
    {
        if (!relatedGraphEntities.Any() || !chunkEntities.Any())
        {
            return 0;
        }

        // Count how many related graph entities are mentioned in the chunk
        var matchCount = 0;
        foreach (var graphEntity in relatedGraphEntities)
        {
            var isMatched = chunkEntities.Any(ce =>
                ce.Text.Equals(graphEntity.Name, StringComparison.OrdinalIgnoreCase));

            if (isMatched)
            {
                matchCount++;
            }
        }

        // Calculate boost proportional to matches
        return matchCount > 0 ? matchCount * GraphBoostFactor : 0;
    }

    private static string? GenerateRelevanceExplanation(
        List<NamedEntity> chunkEntities,
        List<GraphEntity> relatedGraphEntities,
        float graphBoost)
    {
        if (graphBoost == 0)
        {
            return null;
        }

        var matchedEntities = new List<string>();
        foreach (var graphEntity in relatedGraphEntities)
        {
            var isMatched = chunkEntities.Any(ce =>
                ce.Text.Equals(graphEntity.Name, StringComparison.OrdinalIgnoreCase));

            if (isMatched)
            {
                matchedEntities.Add(graphEntity.Name);
            }
        }

        return matchedEntities.Any()
            ? $"Boosted {graphBoost:F2} for matching graph entities: {string.Join(", ", matchedEntities)}"
            : null;
    }

    private static List<GraphEntity> FilterRelevantGraphEntities(
        List<NamedEntity> chunkEntities,
        List<GraphEntity> relatedGraphEntities)
    {
        return relatedGraphEntities
            .Where(ge => chunkEntities.Any(ce =>
                ce.Text.Equals(ge.Name, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    private static string GenerateEntityId(string text, string type)
    {
        // Match the logic in EntityIngestionService
        var input = $"{type}:{text.ToLowerInvariant()}";
        var hashBytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(input));
        var hashString = Convert.ToHexString(hashBytes).ToLowerInvariant();
        return $"entity-{hashString[..16]}";
    }
}
