using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Aegis.Infrastructure.Services.Language;

/// <summary>
/// Pattern-based language detector using common words and character patterns
/// </summary>
public class PatternLanguageDetector : ILanguageDetector
{
    private readonly ILogger<PatternLanguageDetector> _logger;

    // Common words for each language
    private static readonly Dictionary<string, (string Name, HashSet<string> CommonWords)> Languages = new()
    {
        ["en"] = ("English", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "be", "to", "of", "and", "a", "in", "that", "have", "i",
            "it", "for", "not", "on", "with", "he", "as", "you", "do", "at",
            "this", "but", "his", "by", "from", "they", "we", "say", "her", "she",
            "or", "an", "will", "my", "one", "all", "would", "there", "their", "what"
        }),
        ["es"] = ("Spanish", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "el", "la", "de", "que", "y", "a", "en", "un", "ser", "se",
            "no", "haber", "por", "con", "su", "para", "como", "estar", "tener", "le",
            "lo", "todo", "pero", "más", "hacer", "o", "poder", "decir", "este", "ir",
            "otro", "ese", "si", "me", "ya", "ver", "porque", "dar", "cuando", "él"
        }),
        ["fr"] = ("French", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "le", "de", "un", "être", "et", "à", "il", "avoir", "ne", "je",
            "son", "que", "se", "qui", "ce", "dans", "en", "du", "elle", "au",
            "pour", "pas", "que", "vous", "par", "sur", "faire", "plus", "dire", "me",
            "on", "mon", "lui", "nous", "comme", "mais", "pouvoir", "avec", "tout", "y"
        }),
        ["de"] = ("German", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "der", "die", "und", "in", "den", "von", "zu", "das", "mit", "sich",
            "des", "auf", "für", "ist", "im", "dem", "nicht", "ein", "eine", "als",
            "auch", "es", "an", "werden", "aus", "er", "hat", "dass", "sie", "nach",
            "wird", "bei", "einer", "um", "am", "sind", "noch", "wie", "einem", "über"
        }),
        ["ru"] = ("Russian", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "в", "и", "не", "на", "я", "быть", "он", "с", "что", "а",
            "по", "это", "она", "этот", "к", "но", "они", "мы", "как", "из",
            "у", "который", "то", "за", "свой", "что", "ее", "так", "его", "же"
        }),
        ["pt"] = ("Portuguese", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "o", "a", "de", "que", "e", "do", "da", "em", "um", "para",
            "é", "com", "não", "uma", "os", "no", "se", "na", "por", "mais",
            "as", "dos", "como", "mas", "foi", "ao", "ele", "das", "tem", "à"
        }),
        ["it"] = ("Italian", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "il", "di", "e", "la", "a", "che", "per", "un", "in", "è",
            "non", "i", "una", "le", "si", "da", "con", "come", "lo", "questo",
            "sono", "al", "ma", "dal", "dei", "più", "nel", "su", "anche", "alla"
        })
    };

    // Character set patterns
    private static readonly Regex CyrillicPattern = new(@"[а-яА-ЯёЁ]", RegexOptions.Compiled);
    private static readonly Regex ArabicPattern = new(@"[\u0600-\u06FF]", RegexOptions.Compiled);
    private static readonly Regex ChinesePattern = new(@"[\u4E00-\u9FFF]", RegexOptions.Compiled);
    private static readonly Regex JapanesePattern = new(@"[\u3040-\u309F\u30A0-\u30FF]", RegexOptions.Compiled);

    public PatternLanguageDetector(ILogger<PatternLanguageDetector> logger)
    {
        _logger = logger;
    }

    public async Task<Result<LanguageDetectionResult>> DetectAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        if (text == null)
        {
            return Result<LanguageDetectionResult>.Failure(
                Error.Validation("LanguageDetector.NullText", "Text cannot be null"));
        }

        try
        {
            return await Task.Run(() =>
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    return Result<LanguageDetectionResult>.Success(new LanguageDetectionResult
                    {
                        LanguageCode = "unknown",
                        LanguageName = "Unknown",
                        Confidence = 1.0
                    });
                }

                // Check for special character sets first
                if (CyrillicPattern.IsMatch(text))
                {
                    return Result<LanguageDetectionResult>.Success(new LanguageDetectionResult
                    {
                        LanguageCode = "ru",
                        LanguageName = "Russian",
                        Confidence = 0.95
                    });
                }

                if (ArabicPattern.IsMatch(text))
                {
                    return Result<LanguageDetectionResult>.Success(new LanguageDetectionResult
                    {
                        LanguageCode = "ar",
                        LanguageName = "Arabic",
                        Confidence = 0.95
                    });
                }

                if (ChinesePattern.IsMatch(text))
                {
                    return Result<LanguageDetectionResult>.Success(new LanguageDetectionResult
                    {
                        LanguageCode = "zh",
                        LanguageName = "Chinese",
                        Confidence = 0.95
                    });
                }

                if (JapanesePattern.IsMatch(text))
                {
                    return Result<LanguageDetectionResult>.Success(new LanguageDetectionResult
                    {
                        LanguageCode = "ja",
                        LanguageName = "Japanese",
                        Confidence = 0.95
                    });
                }

                // Tokenize text for word-based detection
                var words = text.ToLowerInvariant()
                    .Split(new[] { ' ', '\t', '\n', '\r', '.', ',', '!', '?', ';', ':', '-', '(', ')' },
                        StringSplitOptions.RemoveEmptyEntries);

                if (words.Length == 0)
                {
                    return Result<LanguageDetectionResult>.Success(new LanguageDetectionResult
                    {
                        LanguageCode = "unknown",
                        LanguageName = "Unknown",
                        Confidence = 1.0
                    });
                }

                // Count matches for each language
                var scores = new Dictionary<string, int>();
                foreach (var (code, (_, commonWords)) in Languages)
                {
                    scores[code] = 0;
                }

                foreach (var word in words)
                {
                    foreach (var (code, (_, commonWords)) in Languages)
                    {
                        if (commonWords.Contains(word))
                        {
                            scores[code]++;
                        }
                    }
                }

                // Find the language with the highest score
                var maxScore = scores.Values.Max();
                if (maxScore == 0)
                {
                    // No matches found, default to English
                    return Result<LanguageDetectionResult>.Success(new LanguageDetectionResult
                    {
                        LanguageCode = "en",
                        LanguageName = "English",
                        Confidence = 0.3 // Low confidence
                    });
                }

                var detectedLanguage = scores.First(s => s.Value == maxScore).Key;
                var confidence = Math.Min(1.0, (double)maxScore / words.Length * 2.0);

                return Result<LanguageDetectionResult>.Success(new LanguageDetectionResult
                {
                    LanguageCode = detectedLanguage,
                    LanguageName = Languages[detectedLanguage].Name,
                    Confidence = confidence
                });
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect language");
            return Result<LanguageDetectionResult>.Failure(
                Error.Internal("LanguageDetector.DetectionError", "Failed to detect language"));
        }
    }
}
