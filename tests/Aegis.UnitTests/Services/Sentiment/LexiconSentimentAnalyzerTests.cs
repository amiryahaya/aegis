using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.Sentiment;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aegis.UnitTests.Services.Sentiment;

public class LexiconSentimentAnalyzerTests
{
    private readonly LexiconSentimentAnalyzer _analyzer;

    public LexiconSentimentAnalyzerTests()
    {
        _analyzer = new LexiconSentimentAnalyzer(NullLogger<LexiconSentimentAnalyzer>.Instance);
    }

    [Fact]
    public async Task AnalyzeAsync_WithPositiveText_ShouldReturnPositiveSentiment()
    {
        // Arrange
        var text = "This is excellent news! I'm very happy and excited about the great results.";

        // Act
        var result = await _analyzer.AnalyzeAsync(text);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Sentiment.Should().Be(SentimentLabel.Positive);
        result.Value.PositiveScore.Should().BeGreaterThan(result.Value.NegativeScore);
        result.Value.Confidence.Should().BeGreaterThan(0.5);
    }

    [Fact]
    public async Task AnalyzeAsync_WithNegativeText_ShouldReturnNegativeSentiment()
    {
        // Arrange
        var text = "This is terrible and awful. I hate this bad situation. It's horrible.";

        // Act
        var result = await _analyzer.AnalyzeAsync(text);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Sentiment.Should().Be(SentimentLabel.Negative);
        result.Value.NegativeScore.Should().BeGreaterThan(result.Value.PositiveScore);
        result.Value.Confidence.Should().BeGreaterThan(0.5);
    }

    [Fact]
    public async Task AnalyzeAsync_WithNeutralText_ShouldReturnNeutralSentiment()
    {
        // Arrange
        var text = "The meeting is scheduled for tomorrow at 3 PM in the conference room.";

        // Act
        var result = await _analyzer.AnalyzeAsync(text);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Sentiment.Should().Be(SentimentLabel.Neutral);
        result.Value.NeutralScore.Should().BeGreaterThan(0.3);
    }

    [Fact]
    public async Task AnalyzeAsync_WithMixedSentiment_ShouldDetectMixed()
    {
        // Arrange
        var text = "The product is excellent and I love the features, but the price is terrible and the support is awful.";

        // Act
        var result = await _analyzer.AnalyzeAsync(text);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // Should be mixed or have both positive and negative scores
        result.Value.PositiveScore.Should().BeGreaterThan(0.0);
        result.Value.NegativeScore.Should().BeGreaterThan(0.0);
    }

    [Fact]
    public async Task AnalyzeAsync_WithEmptyText_ShouldReturnNeutral()
    {
        // Arrange
        var text = "";

        // Act
        var result = await _analyzer.AnalyzeAsync(text);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Sentiment.Should().Be(SentimentLabel.Neutral);
    }

    [Fact]
    public async Task AnalyzeAsync_WithNullText_ShouldReturnFailure()
    {
        // Act
        var result = await _analyzer.AnalyzeAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldNormalizeScores()
    {
        // Arrange
        var text = "Amazing wonderful excellent fantastic great superb";

        // Act
        var result = await _analyzer.AnalyzeAsync(text);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var totalScore = result.Value.PositiveScore + result.Value.NegativeScore + result.Value.NeutralScore;
        totalScore.Should().BeApproximately(1.0, 0.01); // Scores should sum to 1.0
    }

    [Fact]
    public async Task AnalyzeAsync_WithThreatIntelligenceText_ShouldAnalyzeCorrectly()
    {
        // Arrange
        var text = "Critical vulnerability detected. Immediate action required to prevent catastrophic failure.";

        // Act
        var result = await _analyzer.AnalyzeAsync(text);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Sentiment.Should().BeOneOf(SentimentLabel.Negative, SentimentLabel.Mixed);
        result.Value.NegativeScore.Should().BeGreaterThan(0.0);
    }

    [Fact]
    public async Task AnalyzeAsync_WithSuccessfulMitigationText_ShouldBePositive()
    {
        // Arrange
        var text = "Successfully mitigated the threat. The system is secure and protected. Excellent response.";

        // Act
        var result = await _analyzer.AnalyzeAsync(text);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Sentiment.Should().Be(SentimentLabel.Positive);
        result.Value.PositiveScore.Should().BeGreaterThan(result.Value.NegativeScore);
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldIncludeConfidenceScore()
    {
        // Arrange
        var text = "This is absolutely wonderful and amazing!";

        // Act
        var result = await _analyzer.AnalyzeAsync(text);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Confidence.Should().BeInRange(0.0, 1.0);
        result.Value.Confidence.Should().BeGreaterThan(0.0);
    }
}
