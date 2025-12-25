using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Sentiment;

/// <summary>
/// Lexicon-based sentiment analyzer using word lists
/// </summary>
public class LexiconSentimentAnalyzer : ISentimentAnalyzer
{
    private readonly ILogger<LexiconSentimentAnalyzer> _logger;

    // Positive sentiment words
    private static readonly HashSet<string> PositiveWords = new(StringComparer.OrdinalIgnoreCase)
    {
        // Common positive words
        "good", "great", "excellent", "amazing", "wonderful", "fantastic", "superb",
        "outstanding", "brilliant", "perfect", "best", "love", "happy", "pleased",
        "delighted", "excited", "thrilled", "satisfied", "successful", "effective",
        "efficient", "impressive", "remarkable", "exceptional", "superior", "valuable",
        "beneficial", "positive", "advantage", "benefit", "success", "win", "victory",

        // Security/Intel positive words
        "secure", "protected", "safe", "mitigated", "resolved", "fixed", "patched",
        "defended", "prevented", "blocked", "stopped", "neutralized", "contained"
    };

    // Negative sentiment words
    private static readonly HashSet<string> NegativeWords = new(StringComparer.OrdinalIgnoreCase)
    {
        // Common negative words
        "bad", "terrible", "awful", "horrible", "poor", "worst", "hate", "dislike",
        "angry", "sad", "unhappy", "disappointed", "frustrated", "annoyed", "upset",
        "concerned", "worried", "problem", "issue", "failure", "fail", "failed",
        "ineffective", "inefficient", "inferior", "worthless", "useless", "negative",

        // Security/Intel negative words
        "vulnerable", "vulnerability", "breach", "compromised", "attack", "threat",
        "malicious", "malware", "exploit", "exploited", "infected", "dangerous",
        "critical", "severe", "urgent", "risk", "exposed", "unauthorized", "suspicious",
        "detected", "intrusion", "incident", "catastrophic", "devastating"
    };

    // Intensifiers
    private static readonly HashSet<string> Intensifiers = new(StringComparer.OrdinalIgnoreCase)
    {
        "very", "extremely", "absolutely", "completely", "totally", "highly",
        "particularly", "especially", "remarkably", "exceptionally"
    };

    // Negations
    private static readonly HashSet<string> Negations = new(StringComparer.OrdinalIgnoreCase)
    {
        "not", "no", "never", "neither", "nor", "none", "nobody", "nothing",
        "nowhere", "hardly", "scarcely", "barely"
    };

    public LexiconSentimentAnalyzer(ILogger<LexiconSentimentAnalyzer> logger)
    {
        _logger = logger;
    }

    public async Task<Result<SentimentResult>> AnalyzeAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        if (text == null)
        {
            return Result<SentimentResult>.Failure(
                Error.Validation("SentimentAnalyzer.NullText", "Text cannot be null"));
        }

        try
        {
            return await Task.Run(() =>
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    return Result<SentimentResult>.Success(new SentimentResult
                    {
                        Sentiment = SentimentLabel.Neutral,
                        Confidence = 1.0,
                        PositiveScore = 0.0,
                        NegativeScore = 0.0,
                        NeutralScore = 1.0
                    });
                }

                // Tokenize text
                var words = text.Split(new[] { ' ', '\t', '\n', '\r', '.', ',', '!', '?', ';', ':' },
                    StringSplitOptions.RemoveEmptyEntries);

                double positiveCount = 0;
                double negativeCount = 0;
                bool previousWasNegation = false;
                double intensifierMultiplier = 1.0;

                for (int i = 0; i < words.Length; i++)
                {
                    var word = words[i].Trim().ToLowerInvariant();

                    // Check for intensifiers
                    if (Intensifiers.Contains(word))
                    {
                        intensifierMultiplier = 1.5;
                        continue;
                    }

                    // Check for negations
                    if (Negations.Contains(word))
                    {
                        previousWasNegation = true;
                        continue;
                    }

                    // Check sentiment
                    var isPositive = PositiveWords.Contains(word);
                    var isNegative = NegativeWords.Contains(word);

                    if (isPositive || isNegative)
                    {
                        var score = intensifierMultiplier;

                        if (previousWasNegation)
                        {
                            // Negation flips sentiment
                            if (isPositive)
                                negativeCount += score;
                            else
                                positiveCount += score;
                        }
                        else
                        {
                            if (isPositive)
                                positiveCount += score;
                            else
                                negativeCount += score;
                        }

                        // Reset modifiers
                        previousWasNegation = false;
                        intensifierMultiplier = 1.0;
                    }
                    else
                    {
                        // Reset modifiers if next word isn't sentiment
                        previousWasNegation = false;
                        intensifierMultiplier = 1.0;
                    }
                }

                // Calculate scores
                var totalWords = words.Length;
                var sentimentWords = positiveCount + negativeCount;

                double positiveScore, negativeScore, neutralScore;

                if (sentimentWords == 0)
                {
                    // No sentiment words found - neutral
                    positiveScore = 0.0;
                    negativeScore = 0.0;
                    neutralScore = 1.0;
                }
                else
                {
                    // Normalize scores
                    var total = positiveCount + negativeCount;
                    positiveScore = positiveCount / total;
                    negativeScore = negativeCount / total;

                    // Neutral score based on ratio of sentiment words to total words
                    var sentimentRatio = sentimentWords / totalWords;
                    neutralScore = Math.Max(0, 1.0 - sentimentRatio);

                    // Renormalize to sum to 1.0
                    var sum = positiveScore + negativeScore + neutralScore;
                    if (sum > 0)
                    {
                        positiveScore /= sum;
                        negativeScore /= sum;
                        neutralScore /= sum;
                    }
                }

                // Determine overall sentiment
                var sentiment = DetermineSentiment(positiveScore, negativeScore, neutralScore);

                // Calculate confidence based on the dominance of the winning sentiment
                var maxScore = Math.Max(Math.Max(positiveScore, negativeScore), neutralScore);
                var confidence = maxScore;

                return Result<SentimentResult>.Success(new SentimentResult
                {
                    Sentiment = sentiment,
                    Confidence = confidence,
                    PositiveScore = positiveScore,
                    NegativeScore = negativeScore,
                    NeutralScore = neutralScore
                });
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze sentiment");
            return Result<SentimentResult>.Failure(
                Error.Internal("SentimentAnalyzer.AnalysisError", "Failed to analyze sentiment"));
        }
    }

    private static SentimentLabel DetermineSentiment(double positive, double negative, double neutral)
    {
        const double threshold = 0.1;

        // If neutral is dominant
        if (neutral > positive && neutral > negative)
        {
            return SentimentLabel.Neutral;
        }

        // If positive and negative are both significant (within threshold of each other)
        if (Math.Abs(positive - negative) < threshold && positive > 0.2 && negative > 0.2)
        {
            return SentimentLabel.Mixed;
        }

        // Otherwise, return the dominant sentiment
        if (positive > negative)
        {
            return SentimentLabel.Positive;
        }
        else if (negative > positive)
        {
            return SentimentLabel.Negative;
        }
        else
        {
            return SentimentLabel.Neutral;
        }
    }
}
