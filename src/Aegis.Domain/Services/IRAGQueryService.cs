using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Service for executing RAG queries end-to-end
/// </summary>
public interface IRAGQueryService
{
    /// <summary>
    /// Execute a RAG query and generate a response
    /// </summary>
    Task<Result<RAGQueryResponse>> QueryAsync(
        string query,
        Guid workspaceId,
        Guid? userId = null,
        Guid? conversationId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute a streaming RAG query
    /// </summary>
    IAsyncEnumerable<Result<RAGStreamChunk>> QueryStreamingAsync(
        string query,
        Guid workspaceId,
        Guid? userId = null,
        Guid? conversationId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Response from a RAG query
/// </summary>
public class RAGQueryResponse
{
    public required string Query { get; init; }
    public required string Response { get; init; }
    public required List<SourceReference> Sources { get; init; }
    public required QueryAnalysis QueryAnalysis { get; init; }
    public required int TokensUsed { get; init; }
    public required TimeSpan ProcessingTime { get; init; }
    public Guid? ConversationId { get; init; }
}

/// <summary>
/// A chunk of streaming RAG response
/// </summary>
public class RAGStreamChunk
{
    public required string Content { get; init; }
    public bool IsComplete { get; init; }
    public List<SourceReference>? Sources { get; init; }
    public QueryAnalysis? QueryAnalysis { get; init; }
}

/// <summary>
/// Reference to a source document
/// </summary>
public class SourceReference
{
    public required Guid DocumentId { get; init; }
    public required string DocumentName { get; init; }
    public required string Content { get; init; }
    public required double Relevance { get; init; }
    public required int ChunkIndex { get; init; }
}
