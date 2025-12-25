using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Service for analyzing sentiment in text
/// </summary>
public interface ISentimentAnalyzer
{
    /// <summary>
    /// Analyzes the sentiment of the provided text
    /// </summary>
    /// <param name="text">The text to analyze</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing sentiment analysis</returns>
    Task<Result<SentimentResult>> AnalyzeAsync(string text, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the result of sentiment analysis
/// </summary>
public class SentimentResult
{
    /// <summary>
    /// Overall sentiment classification
    /// </summary>
    public required SentimentLabel Sentiment { get; init; }

    /// <summary>
    /// Confidence score (0.0 to 1.0)
    /// </summary>
    public required double Confidence { get; init; }

    /// <summary>
    /// Positive sentiment score (0.0 to 1.0)
    /// </summary>
    public double PositiveScore { get; init; }

    /// <summary>
    /// Negative sentiment score (0.0 to 1.0)
    /// </summary>
    public double NegativeScore { get; init; }

    /// <summary>
    /// Neutral sentiment score (0.0 to 1.0)
    /// </summary>
    public double NeutralScore { get; init; }
}

/// <summary>
/// Sentiment classification labels
/// </summary>
public enum SentimentLabel
{
    Positive,
    Negative,
    Neutral,
    Mixed
}
