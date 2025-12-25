using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Interface for parsing documents into text content
/// </summary>
public interface IDocumentParser
{
    /// <summary>
    /// Gets the file extensions supported by this parser (e.g., ".pdf", ".docx")
    /// </summary>
    IReadOnlyCollection<string> SupportedExtensions { get; }

    /// <summary>
    /// Parses a document stream and extracts text content and metadata
    /// </summary>
    /// <param name="stream">The document stream to parse</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Parsed document result containing text and metadata</returns>
    Task<Result<ParsedDocument>> ParseAsync(Stream stream, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the result of parsing a document
/// </summary>
public record ParsedDocument
{
    /// <summary>
    /// The extracted text content from the document
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Metadata extracted from the document (e.g., title, author, page count)
    /// </summary>
    public required Dictionary<string, string> Metadata { get; init; }

    /// <summary>
    /// The number of characters in the extracted text
    /// </summary>
    public int CharacterCount => Text.Length;
}
