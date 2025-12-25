namespace Aegis.Domain.Services;

/// <summary>
/// Service for assembling context from multiple sources for RAG queries
/// </summary>
public interface IRAGContextAssembler
{
    /// <summary>
    /// Assemble context from retrieved chunks, workspace knowledge, and custom instructions
    /// </summary>
    Task<RAGContext> AssembleContextAsync(
        QueryAnalysis queryAnalysis,
        Guid workspaceId,
        int maxChunks = 10,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Assembled context for RAG query
/// </summary>
public class RAGContext
{
    public required QueryAnalysis Query { get; init; }
    public required WorkspaceContext WorkspaceContext { get; init; }
    public List<RetrievedChunk> RetrievedChunks { get; init; } = new();
    public int TotalTokens { get; init; }
    public DateTime AssembledAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Format the complete context as a prompt for the LLM
    /// </summary>
    public string FormatAsPrompt()
    {
        var sections = new List<string>();

        // Workspace context (custom instructions, entities, facts, findings)
        var workspaceText = WorkspaceContext.FormatAsPromptContext();
        if (!string.IsNullOrWhiteSpace(workspaceText))
        {
            sections.Add("# Workspace Context\n" + workspaceText);
        }

        // Retrieved document chunks
        if (RetrievedChunks.Any())
        {
            var chunksText = string.Join("\n\n", RetrievedChunks.Select((chunk, idx) =>
                $"## Source {idx + 1} (Relevance: {chunk.Score:F3})\n" +
                $"Document: {chunk.DocumentName}\n" +
                $"Content:\n{chunk.Content}"));

            sections.Add("# Retrieved Information\n" + chunksText);
        }

        return sections.Any() ? string.Join("\n\n---\n\n", sections) : string.Empty;
    }
}

/// <summary>
/// A retrieved chunk with metadata
/// </summary>
public class RetrievedChunk
{
    public required Guid ChunkId { get; init; }
    public required Guid DocumentId { get; init; }
    public required string DocumentName { get; init; }
    public required string Content { get; init; }
    public required double Score { get; init; }
    public required string RetrievalMethod { get; init; } // "vector", "bm25", "graph", "hybrid"
    public Dictionary<string, object> Metadata { get; init; } = new();
}
