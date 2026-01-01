using System.Text.Json;
using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Agents;

/// <summary>
/// Agent responsible for evaluating response quality, completeness, and faithfulness
/// </summary>
public class EvaluatorAgent : IEvaluatorAgent
{
    private readonly ILLMService _llmService;
    private readonly ILogger<EvaluatorAgent> _logger;

    public string AgentType => "Evaluator";

    public EvaluatorAgent(
        ILLMService llmService,
        ILogger<EvaluatorAgent> logger)
    {
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<Result<AgentResponse>> ExecuteAsync(
        AgentRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Evaluator agent executing: {Task}", request.Task);

            // Extract evaluation parameters from context
            var query = request.Context.GetValueOrDefault("query")?.ToString() ?? "";
            var response = request.Context.GetValueOrDefault("response")?.ToString() ?? "";
            var sources = ExtractSources(request.Context);

            var evaluationRequest = new EvaluationRequest
            {
                Query = query,
                Response = response,
                SourceContexts = sources,
                WorkspaceId = request.WorkspaceId
            };

            var evaluationResult = await EvaluateAsync(evaluationRequest, cancellationToken);

            if (evaluationResult.IsFailure)
            {
                return Result<AgentResponse>.Failure(evaluationResult.Error!);
            }

            var agentResponse = new AgentResponse
            {
                Result = JsonSerializer.Serialize(evaluationResult.Value),
                Confidence = evaluationResult.Value.EvaluationConfidence,
                Metadata = new Dictionary<string, object>
                {
                    { "completenessScore", evaluationResult.Value.CompletenessScore },
                    { "faithfulnessScore", evaluationResult.Value.FaithfulnessScore },
                    { "overallScore", evaluationResult.Value.OverallScore },
                    { "needsRefinement", evaluationResult.Value.NeedsRefinement },
                    { "hasPotentialHallucination", evaluationResult.Value.HasPotentialHallucination }
                },
                ReasoningSteps = evaluationResult.Value.ReasoningSteps
            };

            return Result<AgentResponse>.Success(agentResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Evaluator agent");
            return Result<AgentResponse>.Failure(
                Error.Internal("Evaluator.Error", ex.Message));
        }
    }

    public async Task<Result<EvaluationResult>> EvaluateAsync(
        EvaluationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var reasoningSteps = new List<string>();

            // Validate input
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                return Result<EvaluationResult>.Failure(
                    Error.Validation("Evaluation.QueryRequired", "Query is required for evaluation"));
            }

            reasoningSteps.Add("Validated evaluation request");

            // Calculate completeness score
            var completenessScore = CalculateCompletenessScore(request.Query, request.Response);
            reasoningSteps.Add($"Calculated completeness score: {completenessScore:F2}");

            // Calculate faithfulness score
            var (faithfulnessScore, claimVerifications) = CalculateFaithfulnessScore(
                request.Response, request.SourceContexts);
            reasoningSteps.Add($"Calculated faithfulness score: {faithfulnessScore:F2}");

            // Calculate relevance score
            var relevanceScore = CalculateRelevanceScore(request.Query, request.Response);
            reasoningSteps.Add($"Calculated relevance score: {relevanceScore:F2}");

            // Calculate overall score (weighted average)
            var overallScore = CalculateOverallScore(completenessScore, faithfulnessScore, relevanceScore);
            reasoningSteps.Add($"Calculated overall score: {overallScore:F2}");

            // Check for potential hallucinations
            var hasPotentialHallucination = DetectPotentialHallucination(claimVerifications, faithfulnessScore);
            if (hasPotentialHallucination)
            {
                reasoningSteps.Add("Detected potential hallucination in response");
            }

            // Determine if refinement is needed
            var needsRefinement = overallScore < EvaluationResult.RefinementThreshold;
            if (needsRefinement)
            {
                reasoningSteps.Add($"Response needs refinement (score {overallScore:F2} < threshold {EvaluationResult.RefinementThreshold})");
            }

            // Generate improvement suggestions
            var suggestions = GenerateSuggestions(
                request, completenessScore, faithfulnessScore, relevanceScore, claimVerifications);
            reasoningSteps.Add($"Generated {suggestions.Count} improvement suggestions");

            // Calculate evaluation confidence
            var evaluationConfidence = CalculateEvaluationConfidence(request);
            reasoningSteps.Add($"Evaluation confidence: {evaluationConfidence:F2}");

            var result = new EvaluationResult
            {
                CompletenessScore = completenessScore,
                FaithfulnessScore = faithfulnessScore,
                RelevanceScore = relevanceScore,
                OverallScore = overallScore,
                EvaluationConfidence = evaluationConfidence,
                HasPotentialHallucination = hasPotentialHallucination,
                NeedsRefinement = needsRefinement,
                Suggestions = suggestions,
                ReasoningSteps = reasoningSteps,
                ClaimVerifications = claimVerifications
            };

            _logger.LogInformation(
                "Evaluation complete - Overall: {Overall:F2}, Completeness: {Completeness:F2}, Faithfulness: {Faithfulness:F2}",
                overallScore, completenessScore, faithfulnessScore);

            return Result<EvaluationResult>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during evaluation");
            return Result<EvaluationResult>.Failure(
                Error.Internal("Evaluation.Error", ex.Message));
        }
    }

    private double CalculateCompletenessScore(string query, string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return 0.0;
        }

        // Extract key terms from the query
        var queryTerms = ExtractKeyTerms(query);
        if (queryTerms.Count == 0)
        {
            return 0.5; // Neutral score if no key terms
        }

        // Check how many query terms are addressed in the response
        var responseLower = response.ToLowerInvariant();
        var addressedTerms = queryTerms.Count(term =>
            responseLower.Contains(term.ToLowerInvariant()));

        // Calculate base score from term coverage
        var termCoverage = (double)addressedTerms / queryTerms.Count;

        // Bonus for response length (more detailed responses are typically more complete)
        var lengthBonus = Math.Min(response.Length / 500.0, 0.2);

        // Check for question words and ensure they're addressed
        var questionWords = new[] { "what", "why", "how", "when", "where", "who", "which" };
        var hasQuestionWord = questionWords.Any(q => query.ToLowerInvariant().Contains(q));
        var questionBonus = hasQuestionWord && response.Length > 50 ? 0.1 : 0.0;

        return Math.Min(termCoverage + lengthBonus + questionBonus, 1.0);
    }

    private (double score, List<ClaimVerification> verifications) CalculateFaithfulnessScore(
        string response, List<string> sourceContexts)
    {
        var verifications = new List<ClaimVerification>();

        if (sourceContexts.Count == 0)
        {
            return (0.0, verifications);
        }

        if (string.IsNullOrWhiteSpace(response))
        {
            return (0.0, verifications);
        }

        // Extract claims from the response (simple sentence-based approach)
        var claims = ExtractClaims(response);
        if (claims.Count == 0)
        {
            return (0.5, verifications); // Neutral if no claims detected
        }

        // Combine all source contexts for matching
        var combinedSources = string.Join(" ", sourceContexts).ToLowerInvariant();
        var supportedCount = 0;

        foreach (var claim in claims)
        {
            var claimLower = claim.ToLowerInvariant();
            var claimTerms = ExtractKeyTerms(claim);

            // Check if claim terms are found in sources
            var matchingTerms = claimTerms.Count(term =>
                combinedSources.Contains(term.ToLowerInvariant()));

            var isSupported = claimTerms.Count > 0 &&
                (double)matchingTerms / claimTerms.Count >= 0.5;

            // Find the supporting source index
            int? supportingIndex = null;
            if (isSupported)
            {
                supportingIndex = FindSupportingSourceIndex(claim, sourceContexts);
                supportedCount++;
            }

            verifications.Add(new ClaimVerification
            {
                Claim = claim,
                IsSupported = isSupported,
                SupportingSourceIndex = supportingIndex,
                Confidence = isSupported ? 0.8 : 0.6
            });
        }

        var score = (double)supportedCount / claims.Count;
        return (score, verifications);
    }

    private double CalculateRelevanceScore(string query, string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return 0.0;
        }

        var queryTerms = ExtractKeyTerms(query);
        var responseTerms = ExtractKeyTerms(response);

        if (queryTerms.Count == 0 || responseTerms.Count == 0)
        {
            return 0.5;
        }

        // Calculate Jaccard similarity
        var intersection = queryTerms.Intersect(responseTerms, StringComparer.OrdinalIgnoreCase).Count();
        var union = queryTerms.Union(responseTerms, StringComparer.OrdinalIgnoreCase).Count();

        var jaccardSimilarity = union > 0 ? (double)intersection / union : 0.0;

        // Boost relevance if response contains query terms
        var queryTermsInResponse = queryTerms.Count(t =>
            response.ToLowerInvariant().Contains(t.ToLowerInvariant()));
        var termBoost = (double)queryTermsInResponse / queryTerms.Count * 0.3;

        return Math.Min(jaccardSimilarity + termBoost, 1.0);
    }

    private double CalculateOverallScore(
        double completeness, double faithfulness, double relevance)
    {
        // Weighted average: faithfulness is most important for RAG
        const double completenessWeight = 0.3;
        const double faithfulnessWeight = 0.4;
        const double relevanceWeight = 0.3;

        return (completeness * completenessWeight) +
               (faithfulness * faithfulnessWeight) +
               (relevance * relevanceWeight);
    }

    private bool DetectPotentialHallucination(
        List<ClaimVerification> verifications, double faithfulnessScore)
    {
        // Hallucination detected if:
        // 1. Faithfulness score is low
        // 2. Multiple claims are not supported by sources
        if (faithfulnessScore < 0.3)
        {
            return true;
        }

        if (verifications.Count == 0)
        {
            return false;
        }

        var unsupportedRatio = verifications.Count(v => !v.IsSupported) / (double)verifications.Count;
        return unsupportedRatio > 0.5;
    }

    private List<string> GenerateSuggestions(
        EvaluationRequest request,
        double completeness,
        double faithfulness,
        double relevance,
        List<ClaimVerification> verifications)
    {
        var suggestions = new List<string>();

        if (completeness < 0.7)
        {
            suggestions.Add("Consider expanding the response to address all aspects of the query");

            var queryTerms = ExtractKeyTerms(request.Query);
            var responseLower = request.Response.ToLowerInvariant();
            var missedTerms = queryTerms.Where(t => !responseLower.Contains(t.ToLowerInvariant())).ToList();

            if (missedTerms.Count > 0)
            {
                suggestions.Add($"The response may not address: {string.Join(", ", missedTerms.Take(3))}");
            }
        }

        if (faithfulness < 0.7)
        {
            suggestions.Add("Some claims in the response may not be supported by the source documents");

            var unsupportedClaims = verifications.Where(v => !v.IsSupported).Take(2).ToList();
            foreach (var claim in unsupportedClaims)
            {
                suggestions.Add($"Verify claim: \"{claim.Claim.Substring(0, Math.Min(claim.Claim.Length, 50))}...\"");
            }
        }

        if (relevance < 0.7)
        {
            suggestions.Add("The response may be drifting from the original query topic");
        }

        if (string.IsNullOrWhiteSpace(request.Response) || request.Response.Length < 50)
        {
            suggestions.Add("Consider providing a more detailed response");
        }

        return suggestions;
    }

    private double CalculateEvaluationConfidence(EvaluationRequest request)
    {
        var confidence = 0.5;

        // More source contexts increase confidence
        if (request.SourceContexts.Count > 0)
        {
            confidence += Math.Min(request.SourceContexts.Count * 0.1, 0.3);
        }

        // Longer response allows for better evaluation
        if (!string.IsNullOrWhiteSpace(request.Response))
        {
            confidence += Math.Min(request.Response.Length / 1000.0, 0.2);
        }

        return Math.Min(confidence, 1.0);
    }

    private List<string> ExtractKeyTerms(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<string>();
        }

        // Simple tokenization - split on whitespace and punctuation
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "a", "an", "is", "are", "was", "were", "be", "been", "being",
            "have", "has", "had", "do", "does", "did", "will", "would", "could",
            "should", "may", "might", "must", "shall", "can", "need", "dare",
            "ought", "used", "to", "of", "in", "for", "on", "with", "at", "by",
            "from", "as", "into", "through", "during", "before", "after",
            "above", "below", "between", "under", "again", "further", "then",
            "once", "here", "there", "when", "where", "why", "how", "all",
            "each", "few", "more", "most", "other", "some", "such", "no",
            "nor", "not", "only", "own", "same", "so", "than", "too", "very",
            "just", "and", "but", "if", "or", "because", "until", "while",
            "this", "that", "these", "those", "what", "which", "who", "whom",
            "it", "its", "i", "me", "my", "myself", "we", "our", "you", "your"
        };

        var words = text.Split(new[] { ' ', '.', ',', '!', '?', ';', ':', '-', '(', ')', '[', ']', '"', '\'' },
            StringSplitOptions.RemoveEmptyEntries);

        return words
            .Where(w => w.Length > 2 && !stopWords.Contains(w))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<string> ExtractClaims(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return new List<string>();
        }

        // Simple sentence splitting
        var sentences = response.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);

        return sentences
            .Select(s => s.Trim())
            .Where(s => s.Length > 10) // Filter out very short fragments
            .ToList();
    }

    private int? FindSupportingSourceIndex(string claim, List<string> sources)
    {
        var claimTerms = ExtractKeyTerms(claim);
        var bestMatchIndex = -1;
        var bestMatchScore = 0.0;

        for (int i = 0; i < sources.Count; i++)
        {
            var sourceLower = sources[i].ToLowerInvariant();
            var matchingTerms = claimTerms.Count(t =>
                sourceLower.Contains(t.ToLowerInvariant()));
            var score = claimTerms.Count > 0 ? (double)matchingTerms / claimTerms.Count : 0;

            if (score > bestMatchScore)
            {
                bestMatchScore = score;
                bestMatchIndex = i;
            }
        }

        return bestMatchScore >= 0.3 ? bestMatchIndex : null;
    }

    private List<string> ExtractSources(Dictionary<string, object> context)
    {
        if (context.TryGetValue("sources", out var sourcesObj))
        {
            if (sourcesObj is List<string> sourcesList)
            {
                return sourcesList;
            }
            if (sourcesObj is IEnumerable<object> enumerable)
            {
                return enumerable.Select(o => o?.ToString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList();
            }
        }
        return new List<string>();
    }
}
