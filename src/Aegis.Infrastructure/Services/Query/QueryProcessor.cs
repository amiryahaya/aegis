using System.Text.RegularExpressions;
using Aegis.Domain.Services;

namespace Aegis.Infrastructure.Services.Query;

public class QueryProcessor : IQueryProcessor
{
    private readonly INERService? _nerService;

    private static readonly HashSet<string> QuestionWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "what", "when", "where", "who", "why", "how", "which", "whose", "whom"
    };

    private static readonly HashSet<string> CommandWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "show", "list", "find", "get", "fetch", "retrieve", "display", "give"
    };

    private static readonly HashSet<string> AnalysisWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "analyze", "analysis", "examine", "investigate", "assess", "evaluate"
    };

    private static readonly HashSet<string> SummaryWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "summarize", "summary", "overview", "brief", "recap"
    };

    private static readonly HashSet<string> ComparisonWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "compare", "difference", "versus", "vs", "between", "contrast"
    };

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "is", "are", "was", "were", "be", "been", "being",
        "have", "has", "had", "do", "does", "did", "will", "would", "should",
        "could", "can", "may", "might", "must", "shall", "of", "at", "by",
        "for", "with", "about", "against", "into", "through", "during", "before",
        "after", "above", "below", "to", "from", "up", "down", "in", "out", "on",
        "off", "over", "under", "again", "further", "then", "once"
    };

    public QueryProcessor(INERService? nerService = null)
    {
        _nerService = nerService;
    }

    public async Task<QueryAnalysis> ProcessQueryAsync(
        string query,
        Guid? workspaceId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Query cannot be empty", nameof(query));
        }

        var trimmedQuery = query.Trim();
        var lowercaseQuery = trimmedQuery.ToLowerInvariant();

        // Determine intent
        var intent = DetermineIntent(lowercaseQuery);

        // Extract keywords
        var keywords = ExtractKeywords(trimmedQuery);

        // Extract entities using NER if available
        var entities = new List<string>();
        if (_nerService != null)
        {
            var nerResult = await _nerService.ExtractEntitiesAsync(trimmedQuery);
            if (nerResult.IsSuccess && nerResult.Value != null)
            {
                entities = nerResult.Value.Select(e => e.Text).Distinct().ToList();
            }
        }

        // Determine complexity
        var complexity = DetermineComplexity(trimmedQuery, keywords.Count);

        return new QueryAnalysis
        {
            OriginalQuery = query,
            ProcessedQuery = trimmedQuery,
            Intent = intent,
            ExtractedEntities = entities,
            Keywords = keywords,
            Complexity = complexity,
            Metadata = new Dictionary<string, string>
            {
                ["word_count"] = trimmedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length.ToString(),
                ["has_question_mark"] = trimmedQuery.Contains('?').ToString()
            }
        };
    }

    private static QueryIntent DetermineIntent(string lowercaseQuery)
    {
        var words = lowercaseQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var firstWord = words.FirstOrDefault() ?? "";

        // Check for question
        if (QuestionWords.Contains(firstWord) || lowercaseQuery.Contains('?'))
            return QueryIntent.Question;

        // Check for command
        if (CommandWords.Any(w => lowercaseQuery.StartsWith(w)))
            return QueryIntent.Command;

        // Check for analysis
        if (AnalysisWords.Any(w => lowercaseQuery.Contains(w)))
            return QueryIntent.Analysis;

        // Check for summary
        if (SummaryWords.Any(w => lowercaseQuery.Contains(w)))
            return QueryIntent.Summarization;

        // Check for comparison
        if (ComparisonWords.Any(w => lowercaseQuery.Contains(w)))
            return QueryIntent.Comparison;

        // Check for search patterns
        if (words.Length <= 5 && !lowercaseQuery.Contains('?'))
            return QueryIntent.Search;

        return QueryIntent.Question; // Default to question
    }

    private static List<string> ExtractKeywords(string query)
    {
        // Remove punctuation except hyphens
        var cleaned = Regex.Replace(query, @"[^\w\s-]", " ");

        // Split into words
        var words = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Filter out stop words and short words
        var keywords = words
            .Where(w => !StopWords.Contains(w))
            .Where(w => w.Length > 2)
            .Select(w => w.ToLowerInvariant())
            .Distinct()
            .ToList();

        return keywords;
    }

    private static QueryComplexity DetermineComplexity(string query, int keywordCount)
    {
        var wordCount = query.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

        // Simple: Short query with few keywords
        if (wordCount <= 5 && keywordCount <= 3)
            return QueryComplexity.Simple;

        // Complex: Long query, many keywords, or complex patterns
        if (wordCount > 20 ||
            keywordCount > 10 ||
            query.Contains(" and ") && query.Contains(" or ") ||
            Regex.IsMatch(query, @"\b(compare|analyze|evaluate)\b", RegexOptions.IgnoreCase))
        {
            return QueryComplexity.Complex;
        }

        // Medium: Everything else
        return QueryComplexity.Medium;
    }
}
