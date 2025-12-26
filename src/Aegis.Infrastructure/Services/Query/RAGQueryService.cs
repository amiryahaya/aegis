using System.Diagnostics;
using System.Runtime.CompilerServices;
using Aegis.Domain.Common;
using Aegis.Domain.Entities;
using Aegis.Domain.Repositories;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Query;

public class RAGQueryService : IRAGQueryService
{
    private readonly IQueryProcessor _queryProcessor;
    private readonly IRAGContextAssembler _contextAssembler;
    private readonly ILLMService _llmService;
    private readonly IQueryHistoryRepository? _queryHistoryRepository;
    private readonly ILogger<RAGQueryService> _logger;

    public RAGQueryService(
        IQueryProcessor queryProcessor,
        IRAGContextAssembler contextAssembler,
        ILLMService llmService,
        ILogger<RAGQueryService> logger,
        IQueryHistoryRepository? queryHistoryRepository = null)
    {
        _queryProcessor = queryProcessor;
        _contextAssembler = contextAssembler;
        _llmService = llmService;
        _logger = logger;
        _queryHistoryRepository = queryHistoryRepository;
    }

    public async Task<Result<RAGQueryResponse>> QueryAsync(
        string query,
        Guid workspaceId,
        Guid? userId = null,
        Guid? conversationId = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Processing RAG query for workspace {WorkspaceId}: {Query}", workspaceId, query);

            // Step 1: Process query
            var queryAnalysis = await _queryProcessor.ProcessQueryAsync(query, workspaceId, cancellationToken);

            // Step 2: Assemble context
            var ragContext = await _contextAssembler.AssembleContextAsync(
                queryAnalysis,
                workspaceId,
                maxChunks: 10,
                cancellationToken);

            _logger.LogInformation("Assembled context with {ChunkCount} chunks and {TokenCount} estimated tokens",
                ragContext.RetrievedChunks.Count, ragContext.TotalTokens);

            // Step 3: Build contexts for LLM
            var contexts = new List<string>();

            // Add workspace context first
            var workspaceContextText = ragContext.WorkspaceContext.FormatAsPromptContext();
            if (!string.IsNullOrWhiteSpace(workspaceContextText))
            {
                contexts.Add(workspaceContextText);
            }

            // Add retrieved chunks
            contexts.AddRange(ragContext.RetrievedChunks.Select(c => c.Content));

            // Step 4: Generate response
            var llmResult = await _llmService.GenerateRAGResponseAsync(
                queryAnalysis.ProcessedQuery,
                contexts,
                cancellationToken);

            if (llmResult.IsFailure)
            {
                return Result<RAGQueryResponse>.Failure(llmResult.Error!);
            }

            // Step 5: Build source references
            var sources = ragContext.RetrievedChunks.Select(chunk => new SourceReference
            {
                DocumentId = chunk.DocumentId,
                DocumentName = chunk.DocumentName,
                Content = chunk.Content,
                Relevance = chunk.Score,
                ChunkIndex = (int)chunk.Metadata.GetValueOrDefault("chunk_index", 0)
            }).ToList();

            stopwatch.Stop();

            var response = new RAGQueryResponse
            {
                Query = query,
                Response = llmResult.Value!.Response,
                Sources = sources,
                QueryAnalysis = queryAnalysis,
                TokensUsed = ragContext.TotalTokens,
                ProcessingTime = stopwatch.Elapsed,
                ConversationId = conversationId
            };

            _logger.LogInformation("RAG query completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

            // Step 6: Save query history (if repository available and userId provided)
            if (_queryHistoryRepository != null && userId.HasValue)
            {
                try
                {
                    var retrievalMethod = ragContext.RetrievedChunks.FirstOrDefault()?.RetrievalMethod ?? "unknown";
                    var queryHistory = QueryHistory.Create(
                        workspaceId,
                        userId.Value,
                        query,
                        response.Response,
                        ragContext.TotalTokens,
                        stopwatch.Elapsed,
                        ragContext.RetrievedChunks.Count,
                        retrievalMethod,
                        conversationId);

                    await _queryHistoryRepository.AddAsync(queryHistory, cancellationToken);
                }
                catch (Exception ex)
                {
                    // Log but don't fail the query if history saving fails
                    _logger.LogWarning(ex, "Failed to save query history");
                }
            }

            return Result<RAGQueryResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing RAG query");
            return Result<RAGQueryResponse>.Failure(
                Error.Internal("RAG.QueryError", $"Failed to process query: {ex.Message}"));
        }
    }

    public async IAsyncEnumerable<Result<RAGStreamChunk>> QueryStreamingAsync(
        string query,
        Guid workspaceId,
        Guid? userId = null,
        Guid? conversationId = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        QueryAnalysis? queryAnalysis = null;
        List<SourceReference>? sources = null;
        string? prompt = null;

        // Step 1-4: Prepare context (capture errors without yielding in catch)
        Exception? preparationError = null;

        try
        {
            _logger.LogInformation("Processing streaming RAG query for workspace {WorkspaceId}: {Query}", workspaceId, query);

            // Step 1: Process query
            queryAnalysis = await _queryProcessor.ProcessQueryAsync(query, workspaceId, cancellationToken);

            // Step 2: Assemble context
            var ragContext = await _contextAssembler.AssembleContextAsync(
                queryAnalysis,
                workspaceId,
                maxChunks: 10,
                cancellationToken);

            // Step 3: Build contexts
            var contexts = new List<string>();
            var workspaceContextText = ragContext.WorkspaceContext.FormatAsPromptContext();
            if (!string.IsNullOrWhiteSpace(workspaceContextText))
            {
                contexts.Add(workspaceContextText);
            }
            contexts.AddRange(ragContext.RetrievedChunks.Select(c => c.Content));

            // Build source references
            sources = ragContext.RetrievedChunks.Select(chunk => new SourceReference
            {
                DocumentId = chunk.DocumentId,
                DocumentName = chunk.DocumentName,
                Content = chunk.Content,
                Relevance = chunk.Score,
                ChunkIndex = (int)chunk.Metadata.GetValueOrDefault("chunk_index", 0)
            }).ToList();

            // Step 4: Build prompt
            prompt = BuildRAGPrompt(queryAnalysis.ProcessedQuery, contexts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error preparing streaming RAG query");
            preparationError = ex;
        }

        // Yield error outside of try-catch
        if (preparationError != null)
        {
            yield return Result<RAGStreamChunk>.Failure(
                Error.Internal("RAG.StreamingError", $"Failed to prepare streaming query: {preparationError.Message}"));
            yield break;
        }

        // Step 5: Stream response
        await foreach (var chunk in _llmService.GenerateStreamingResponseAsync(prompt!, cancellationToken))
        {
            if (chunk.IsFailure)
            {
                yield return Result<RAGStreamChunk>.Failure(chunk.Error!);
                yield break;
            }

            yield return Result<RAGStreamChunk>.Success(new RAGStreamChunk
            {
                Content = chunk.Value!,
                IsComplete = false
            });
        }

        // Send final chunk with metadata
        yield return Result<RAGStreamChunk>.Success(new RAGStreamChunk
        {
            Content = "",
            IsComplete = true,
            Sources = sources,
            QueryAnalysis = queryAnalysis
        });
    }

    private static string BuildRAGPrompt(string query, List<string> contexts)
    {
        var prompt = "You are a helpful AI assistant. Answer the user's question based on the provided context.\n\n";
        prompt += "Context:\n";

        for (int i = 0; i < contexts.Count; i++)
        {
            prompt += $"[{i + 1}] {contexts[i]}\n\n";
        }

        prompt += "Instructions:\n";
        prompt += "- Answer the question using ONLY the information from the context above.\n";
        prompt += "- Cite your sources using [1], [2], etc. when referencing context.\n";
        prompt += "- If the context doesn't contain enough information, say so.\n";
        prompt += "- Be concise but complete.\n\n";
        prompt += $"Question: {query}\n";

        return prompt;
    }
}
