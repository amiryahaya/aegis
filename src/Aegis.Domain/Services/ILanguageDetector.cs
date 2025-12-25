using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Service for detecting the language of text
/// </summary>
public interface ILanguageDetector
{
    /// <summary>
    /// Detects the language of the provided text
    /// </summary>
    /// <param name="text">The text to analyze</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing language detection results</returns>
    Task<Result<LanguageDetectionResult>> DetectAsync(string text, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the result of language detection
/// </summary>
public class LanguageDetectionResult
{
    /// <summary>
    /// ISO 639-1 language code (e.g., "en", "es", "fr")
    /// </summary>
    public required string LanguageCode { get; init; }

    /// <summary>
    /// Full language name (e.g., "English", "Spanish", "French")
    /// </summary>
    public required string LanguageName { get; init; }

    /// <summary>
    /// Confidence score (0.0 to 1.0)
    /// </summary>
    public required double Confidence { get; init; }

    /// <summary>
    /// Alternative language detections with lower confidence
    /// </summary>
    public IReadOnlyList<(string Code, string Name, double Confidence)> Alternatives { get; init; }
        = Array.Empty<(string, string, double)>();
}
