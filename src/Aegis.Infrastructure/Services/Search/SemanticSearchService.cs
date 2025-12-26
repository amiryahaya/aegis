using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Search;

/// <summary>
/// Semantic search service combining embedding generation and vector search
/// </summary>
public class SemanticSearchService : ISemanticSearchService
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStore _vectorStore;
    private readonly ILogger<SemanticSearchService> _logger;

    public SemanticSearchService(
        IEmbeddingService embeddingService,
        IVectorStore vectorStore,
        ILogger<SemanticSearchService> logger)
    {
        _embeddingService = embeddingService;
        _vectorStore = vectorStore;
        _logger = logger;
    }

    public async Task<Result<List<SemanticSearchResult>>> SearchAsync(
        string query,
        Guid workspaceId,
        int topK = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Generate embedding for the query
            var embeddingResult = await _embeddingService.GenerateEmbeddingAsync(query, cancellationToken);
            if (embeddingResult.IsFailure)
            {
                _logger.LogWarning("Failed to generate embedding for query: {Error}", embeddingResult.Error?.Message ?? "Unknown error");
                return Result<List<SemanticSearchResult>>.Failure(embeddingResult.Error!);
            }

            // Search vector store
            var collectionName = $"workspace-{workspaceId}";
            var searchResult = await _vectorStore.SearchAsync(
                collectionName,
                embeddingResult.Value,
                topK,
                scoreThreshold: 0.5f,
                filter: null,
                cancellationToken);

            if (searchResult.IsFailure)
            {
                _logger.LogWarning("Vector search failed: {Error}", searchResult.Error?.Message ?? "Unknown error");
                return Result<List<SemanticSearchResult>>.Failure(searchResult.Error!);
            }

            // Map vector search results to semantic search results
            var results = searchResult.Value.Select(r => new SemanticSearchResult
            {
                DocumentId = r.Id,
                Title = r.Metadata.TryGetValue("title", out var title) ? title : "Untitled",
                Content = r.Metadata.TryGetValue("content", out var content) ? content : "",
                Score = r.Score,
                Metadata = r.Metadata
            }).ToList();

            _logger.LogInformation(
                "Semantic search completed: Query='{Query}', Results={Count}",
                query, results.Count);

            return Result<List<SemanticSearchResult>>.Success(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing semantic search");
            return Result<List<SemanticSearchResult>>.Failure(
                Error.Internal("SemanticSearch.Error", ex.Message));
        }
    }
}
