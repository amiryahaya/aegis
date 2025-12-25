using System.Diagnostics;
using System.Runtime.CompilerServices;
using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Query;

public class RAGQueryService : IRAGQueryService
{
    private readonly IQueryProcessor _queryProcessor;
    private readonly IRAGContextAssembler _contextAssembler;
    private readonly ILLMService _llmService;
    private readonly ILogger<RAGQueryService> _logger;

    public RAGQueryService(
        IQueryProcessor queryProcessor,
        IRAGContextAssembler contextAssembler,
        ILLMService llmService,
        ILogger<RAGQueryService> logger)
    {
        _queryProcessor = queryProcessor;
        _contextAssembler = contextAssembler;
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<Result<RAGQueryResponse>> QueryAsync(
        string query,
        Guid workspaceId,
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

            return Result<RAGQueryResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing RAG query");
            return Result<RAGQueryResponse>.Failure(
                Error.Failure("RAG.QueryError", $"Failed to process query: {ex.Message}"));
        }
    }

    public async IAsyncEnumerable<Result<RAGStreamChunk>> QueryStreamingAsync(
        string query,
        Guid workspaceId,
        Guid? conversationId = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        QueryAnalysis? queryAnalysis = null;
        List<SourceReference>? sources = null;

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
            var prompt = BuildRAGPrompt(queryAnalysis.ProcessedQuery, contexts);

            // Step 5: Stream response
            await foreach (var chunk in _llmService.GenerateStreamingResponseAsync(prompt, cancellationToken))
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in streaming RAG query");
            yield return Result<RAGStreamChunk>.Failure(
                Error.Failure("RAG.StreamingError", $"Failed to process streaming query: {ex.Message}"));
        }
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
