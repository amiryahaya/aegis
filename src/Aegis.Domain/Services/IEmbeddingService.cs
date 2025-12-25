using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Interface for generating text embeddings
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Gets the dimension of the embedding vectors produced by this service
    /// </summary>
    int EmbeddingDimension { get; }

    /// <summary>
    /// Generates an embedding vector for a single text
    /// </summary>
    /// <param name="text">The text to embed</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The embedding vector</returns>
    Task<Result<float[]>> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates embedding vectors for multiple texts in batch
    /// </summary>
    /// <param name="texts">The texts to embed</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The embedding vectors</returns>
    Task<Result<List<float[]>>> GenerateEmbeddingsAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default);
}

/// <summary>
/// Configuration options for embedding service
/// </summary>
public record EmbeddingOptions
{
    /// <summary>
    /// Path to the ONNX model file
    /// </summary>
    public required string ModelPath { get; init; }

    /// <summary>
    /// Path to the tokenizer vocabulary file
    /// </summary>
    public required string VocabularyPath { get; init; }

    /// <summary>
    /// Maximum sequence length for tokenization
    /// </summary>
    public int MaxSequenceLength { get; init; } = 512;

    /// <summary>
    /// Batch size for processing multiple texts
    /// </summary>
    public int BatchSize { get; init; } = 32;
}
