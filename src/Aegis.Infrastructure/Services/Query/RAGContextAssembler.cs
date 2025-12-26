using Aegis.Domain.Repositories;
using Aegis.Domain.Services;

namespace Aegis.Infrastructure.Services.Query;

public class RAGContextAssembler : IRAGContextAssembler
{
    private readonly IHybridRetriever _hybridRetriever;
    private readonly IWorkspaceContextService _workspaceContextService;
    private readonly IDocumentRepository _documentRepository;
    private readonly IRerankerService? _rerankerService;

    public RAGContextAssembler(
        IHybridRetriever hybridRetriever,
        IWorkspaceContextService workspaceContextService,
        IDocumentRepository documentRepository,
        IRerankerService? rerankerService = null)
    {
        _hybridRetriever = hybridRetriever;
        _workspaceContextService = workspaceContextService;
        _documentRepository = documentRepository;
        _rerankerService = rerankerService;
    }

    public async Task<RAGContext> AssembleContextAsync(
        QueryAnalysis queryAnalysis,
        Guid workspaceId,
        int maxChunks = 10,
        CancellationToken cancellationToken = default)
    {
        // Get workspace context (custom instructions, entities, findings, facts)
        var workspaceContext = await _workspaceContextService.GetRelevantContextAsync(
            workspaceId,
            queryAnalysis.ProcessedQuery,
            maxEntities: 5,
            maxFindings: 3,
            maxFacts: 5,
            cancellationToken);

        // Retrieve relevant chunks using hybrid retrieval
        var collectionName = $"workspace_{workspaceId}";
        var retrievalResult = await _hybridRetriever.RetrieveAsync(
            collectionName,
            queryAnalysis.ProcessedQuery,
            limit: maxChunks,
            cancellationToken: cancellationToken);

        // Convert retrieval results to retrieved chunks with document metadata
        var retrievedChunks = new List<RetrievedChunk>();

        if (retrievalResult.IsSuccess && retrievalResult.Value != null)
        {
            foreach (var result in retrievalResult.Value)
            {
                // Extract document ID from metadata
                if (result.Metadata.TryGetValue("document_id", out var docIdStr) &&
                    Guid.TryParse(docIdStr, out var documentId))
                {
                    // Get document metadata
                    var document = await _documentRepository.GetByIdAsync(documentId, cancellationToken);

                    if (document != null)
                    {
                        retrievedChunks.Add(new RetrievedChunk
                        {
                            ChunkId = result.Id,
                            DocumentId = documentId,
                            DocumentName = document.FileName,
                            Content = result.Text,
                            Score = result.Score,
                            RetrievalMethod = "hybrid",
                            Metadata = new Dictionary<string, object>
                            {
                                ["chunk_index"] = result.Metadata.GetValueOrDefault("chunk_index", "0"),
                                ["document_type"] = document.ContentType,
                                ["uploaded_at"] = document.CreatedAt,
                                ["vector_score"] = result.VectorScore ?? 0,
                                ["bm25_score"] = result.BM25Score ?? 0
                            }
                        });
                    }
                }
            }
        }

        // Apply reranking if reranker service is available
        if (_rerankerService != null && retrievedChunks.Count > 0)
        {
            var documents = retrievedChunks.Select(c => c.Content).ToList();
            var rerankResult = await _rerankerService.RerankAsync(
                queryAnalysis.ProcessedQuery,
                documents,
                topK: Math.Min(maxChunks, retrievedChunks.Count),
                cancellationToken);

            if (rerankResult.IsSuccess && rerankResult.Value != null)
            {
                // Create a new list with reranked chunks in order
                var rerankedChunks = new List<RetrievedChunk>();
                foreach (var rankedDoc in rerankResult.Value)
                {
                    var originalChunk = retrievedChunks[rankedDoc.Index];

                    // Update the chunk with reranked score and method
                    rerankedChunks.Add(new RetrievedChunk
                    {
                        ChunkId = originalChunk.ChunkId,
                        DocumentId = originalChunk.DocumentId,
                        DocumentName = originalChunk.DocumentName,
                        Content = originalChunk.Content,
                        Score = rankedDoc.RelevanceScore,
                        RetrievalMethod = "reranked",
                        Metadata = new Dictionary<string, object>(originalChunk.Metadata)
                        {
                            ["original_score"] = originalChunk.Score,
                            ["reranker_score"] = rankedDoc.RelevanceScore
                        }
                    });
                }

                retrievedChunks = rerankedChunks;
            }
        }

        // Estimate token count (rough estimate: ~4 chars per token)
        var totalTokens = EstimateTokens(workspaceContext, retrievedChunks);

        return new RAGContext
        {
            Query = queryAnalysis,
            WorkspaceContext = workspaceContext,
            RetrievedChunks = retrievedChunks,
            TotalTokens = totalTokens,
            AssembledAt = DateTime.UtcNow
        };
    }

    private static int EstimateTokens(WorkspaceContext workspaceContext, List<RetrievedChunk> chunks)
    {
        var contextText = workspaceContext.FormatAsPromptContext();
        var chunksText = string.Join("\n", chunks.Select(c => c.Content));
        var totalChars = contextText.Length + chunksText.Length;

        // Rough estimate: 1 token ≈ 4 characters for English text
        return totalChars / 4;
    }
}
