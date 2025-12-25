using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Interface for Large Language Model services
/// </summary>
public interface ILLMService
{
    /// <summary>
    /// Generates a response from the LLM for a given prompt
    /// </summary>
    /// <param name="prompt">The prompt to send to the LLM</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<Result<string>> GenerateResponseAsync(
        string prompt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a RAG (Retrieval-Augmented Generation) response with citations
    /// </summary>
    /// <param name="query">The user's query</param>
    /// <param name="contexts">Retrieved context passages</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<Result<RAGResponse>> GenerateRAGResponseAsync(
        string query,
        IEnumerable<string> contexts,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a streaming response from the LLM
    /// </summary>
    /// <param name="prompt">The prompt to send to the LLM</param>
    /// <param name="cancellationToken">Cancellation token</param>
    IAsyncEnumerable<Result<string>> GenerateStreamingResponseAsync(
        string prompt,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a RAG response with citations
/// </summary>
public record RAGResponse
{
    /// <summary>
    /// The generated response text
    /// </summary>
    public required string Response { get; init; }

    /// <summary>
    /// Citations to source contexts
    /// </summary>
    public List<Citation> Citations { get; init; } = new();
}

/// <summary>
/// Represents a citation to a source context
/// </summary>
public record Citation
{
    /// <summary>
    /// Index of the cited context in the original context list
    /// </summary>
    public required int Index { get; init; }

    /// <summary>
    /// The cited text
    /// </summary>
    public required string Text { get; init; }
}
