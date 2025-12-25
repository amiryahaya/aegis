using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.Language;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aegis.UnitTests.Services.Language;

public class PatternLanguageDetectorTests
{
    private readonly PatternLanguageDetector _detector;

    public PatternLanguageDetectorTests()
    {
        _detector = new PatternLanguageDetector(NullLogger<PatternLanguageDetector>.Instance);
    }

    [Fact]
    public async Task DetectAsync_WithEnglishText_ShouldDetectEnglish()
    {
        // Arrange
        var text = "This is a sample text written in English. It contains common English words and phrases.";

        // Act
        var result = await _detector.DetectAsync(text);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.LanguageCode.Should().Be("en");
        result.Value.LanguageName.Should().Be("English");
        result.Value.Confidence.Should().BeGreaterThan(0.5);
    }

    [Fact]
    public async Task DetectAsync_WithSpanishText_ShouldDetectSpanish()
    {
        // Arrange
        var text = "Este es un texto de ejemplo escrito en español. Contiene palabras y frases comunes en español.";

        // Act
        var result = await _detector.DetectAsync(text);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.LanguageCode.Should().Be("es");
        result.Value.LanguageName.Should().Be("Spanish");
        result.Value.Confidence.Should().BeGreaterThan(0.5);
    }

    [Fact]
    public async Task DetectAsync_WithFrenchText_ShouldDetectFrench()
    {
        // Arrange
        var text = "Ceci est un exemple de texte écrit en français. Il contient des mots et des phrases françaises.";

        // Act
        var result = await _detector.DetectAsync(text);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.LanguageCode.Should().Be("fr");
        result.Value.LanguageName.Should().Be("French");
        result.Value.Confidence.Should().BeGreaterThan(0.5);
    }

    [Fact]
    public async Task DetectAsync_WithGermanText_ShouldDetectGerman()
    {
        // Arrange
        var text = "Dies ist ein Beispieltext auf Deutsch. Es enthält deutsche Wörter und Phrasen.";

        // Act
        var result = await _detector.DetectAsync(text);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.LanguageCode.Should().Be("de");
        result.Value.LanguageName.Should().Be("German");
        result.Value.Confidence.Should().BeGreaterThan(0.5);
    }

    [Fact]
    public async Task DetectAsync_WithShortText_ShouldStillDetect()
    {
        // Arrange
        var text = "Hello world";

        // Act
        var result = await _detector.DetectAsync(text);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.LanguageCode.Should().NotBeNullOrEmpty();
        result.Value.LanguageName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task DetectAsync_WithMixedLanguageText_ShouldDetectPrimary()
    {
        // Arrange
        var text = "This is primarily an English text with some palabras en español mixed in.";

        // Act
        var result = await _detector.DetectAsync(text);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.LanguageCode.Should().Be("en"); // English is dominant
    }

    [Fact]
    public async Task DetectAsync_WithEmptyText_ShouldReturnUnknown()
    {
        // Arrange
        var text = "";

        // Act
        var result = await _detector.DetectAsync(text);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.LanguageCode.Should().Be("unknown");
    }

    [Fact]
    public async Task DetectAsync_WithNullText_ShouldReturnFailure()
    {
        // Act
        var result = await _detector.DetectAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task DetectAsync_ShouldIncludeConfidenceScore()
    {
        // Arrange
        var text = "The quick brown fox jumps over the lazy dog.";

        // Act
        var result = await _detector.DetectAsync(text);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Confidence.Should().BeInRange(0.0, 1.0);
    }

    [Fact]
    public async Task DetectAsync_WithCyrillicText_ShouldDetectRussian()
    {
        // Arrange
        var text = "Это пример текста на русском языке.";

        // Act
        var result = await _detector.DetectAsync(text);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.LanguageCode.Should().Be("ru");
        result.Value.LanguageName.Should().Be("Russian");
    }
}
