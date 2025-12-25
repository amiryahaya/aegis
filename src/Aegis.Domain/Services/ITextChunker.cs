namespace Aegis.Domain.Services;

/// <summary>
/// Interface for splitting text into chunks for processing and embedding
/// </summary>
public interface ITextChunker
{
    /// <summary>
    /// Splits text into chunks using the configured strategy
    /// </summary>
    /// <param name="text">The text to chunk</param>
    /// <param name="metadata">Optional metadata to attach to each chunk</param>
    /// <returns>List of text chunks</returns>
    Task<List<TextChunk>> ChunkAsync(string text, Dictionary<string, string>? metadata = null);
}

/// <summary>
/// Represents a chunk of text with metadata
/// </summary>
public record TextChunk
{
    /// <summary>
    /// The text content of the chunk
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// The position of this chunk in the original text (0-based index)
    /// </summary>
    public required int Index { get; init; }

    /// <summary>
    /// The character offset where this chunk starts in the original text
    /// </summary>
    public required int StartOffset { get; init; }

    /// <summary>
    /// The character offset where this chunk ends in the original text
    /// </summary>
    public required int EndOffset { get; init; }

    /// <summary>
    /// Additional metadata for the chunk
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = new();
}

/// <summary>
/// Configuration options for text chunking
/// </summary>
public record ChunkingOptions
{
    /// <summary>
    /// Maximum size of each chunk in characters
    /// </summary>
    public int MaxChunkSize { get; init; } = 512;

    /// <summary>
    /// Number of characters to overlap between chunks
    /// </summary>
    public int ChunkOverlap { get; init; } = 50;

    /// <summary>
    /// The chunking strategy to use
    /// </summary>
    public ChunkingStrategy Strategy { get; init; } = ChunkingStrategy.Sentence;
}

/// <summary>
/// Strategies for splitting text into chunks
/// </summary>
public enum ChunkingStrategy
{
    /// <summary>
    /// Split on fixed character boundaries
    /// </summary>
    FixedSize,

    /// <summary>
    /// Split on sentence boundaries
    /// </summary>
    Sentence,

    /// <summary>
    /// Split on paragraph boundaries
    /// </summary>
    Paragraph,

    /// <summary>
    /// Semantic chunking based on topic similarity
    /// </summary>
    Semantic
}
