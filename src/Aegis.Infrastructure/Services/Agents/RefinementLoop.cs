using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Agents;

/// <summary>
/// Iteratively refines responses based on evaluation feedback
/// </summary>
public class RefinementLoop : IRefinementLoop
{
    private readonly IEvaluatorAgent _evaluatorAgent;
    private readonly ILLMService _llmService;
    private readonly ILogger<RefinementLoop> _logger;

    public RefinementLoop(
        IEvaluatorAgent evaluatorAgent,
        ILLMService llmService,
        ILogger<RefinementLoop> logger)
    {
        _evaluatorAgent = evaluatorAgent;
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<Result<RefinementResult>> RefineAsync(
        RefinementRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var reasoningSteps = new List<string>();
            var refinementHistory = new List<RefinementAttempt>();
            var errors = new List<string>();

            // Validate input
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                return Result<RefinementResult>.Failure(
                    Error.Validation("Refinement.QueryRequired", "Query is required for refinement"));
            }

            reasoningSteps.Add("Starting refinement process");

            // Evaluate the original response
            var currentResponse = request.OriginalResponse;
            var evaluationRequest = CreateEvaluationRequest(request, currentResponse);
            var evaluationResult = await _evaluatorAgent.EvaluateAsync(evaluationRequest, cancellationToken);

            if (evaluationResult.IsFailure)
            {
                return Result<RefinementResult>.Failure(evaluationResult.Error!);
            }

            var initialScore = evaluationResult.Value.OverallScore;
            var currentScore = initialScore;
            reasoningSteps.Add($"Initial evaluation score: {initialScore:F2}");

            // Record initial state
            refinementHistory.Add(new RefinementAttempt
            {
                Iteration = 0,
                Response = currentResponse,
                EvaluationScore = initialScore,
                SuggestionsUsed = new List<string>()
            });

            // Check if refinement is needed
            if (!evaluationResult.Value.NeedsRefinement && currentScore >= request.TargetScore)
            {
                reasoningSteps.Add("Response meets quality threshold, no refinement needed");

                return Result<RefinementResult>.Success(new RefinementResult
                {
                    FinalResponse = currentResponse,
                    WasRefined = false,
                    RefinementIterations = 0,
                    ReachedMaxIterations = false,
                    InitialScore = initialScore,
                    FinalScore = currentScore,
                    HadErrors = false,
                    RefinementHistory = refinementHistory,
                    ReasoningSteps = reasoningSteps
                });
            }

            // Refinement loop
            var iteration = 0;
            var reachedMaxIterations = false;
            var currentSuggestions = evaluationResult.Value.Suggestions;

            while (iteration < request.MaxIterations)
            {
                iteration++;
                reasoningSteps.Add($"Starting refinement iteration {iteration}");

                // Generate improved response using LLM
                var refinementPrompt = BuildRefinementPrompt(
                    request.Query,
                    currentResponse,
                    request.SourceContexts,
                    currentSuggestions,
                    evaluationResult.Value);

                var llmResult = await _llmService.GenerateResponseAsync(refinementPrompt, cancellationToken);

                if (llmResult.IsFailure)
                {
                    _logger.LogWarning("LLM refinement failed: {Error}", llmResult.Error?.Message);
                    errors.Add($"Iteration {iteration}: {llmResult.Error?.Message}");
                    reasoningSteps.Add($"LLM failed on iteration {iteration}, returning best response so far");

                    return Result<RefinementResult>.Success(new RefinementResult
                    {
                        FinalResponse = currentResponse,
                        WasRefined = iteration > 1,
                        RefinementIterations = iteration - 1,
                        ReachedMaxIterations = false,
                        InitialScore = initialScore,
                        FinalScore = currentScore,
                        HadErrors = true,
                        Errors = errors,
                        RefinementHistory = refinementHistory,
                        ReasoningSteps = reasoningSteps
                    });
                }

                var refinedResponse = llmResult.Value;
                reasoningSteps.Add($"Generated refined response (length: {refinedResponse.Length})");

                // Evaluate the refined response
                var newEvaluationRequest = CreateEvaluationRequest(request, refinedResponse);
                evaluationResult = await _evaluatorAgent.EvaluateAsync(newEvaluationRequest, cancellationToken);

                if (evaluationResult.IsFailure)
                {
                    errors.Add($"Iteration {iteration}: Evaluation failed");
                    reasoningSteps.Add("Evaluation failed, returning best response so far");
                    break;
                }

                var newScore = evaluationResult.Value.OverallScore;
                reasoningSteps.Add($"Iteration {iteration} score: {newScore:F2}");

                // Record this refinement attempt
                refinementHistory.Add(new RefinementAttempt
                {
                    Iteration = iteration,
                    Response = refinedResponse,
                    EvaluationScore = newScore,
                    SuggestionsUsed = currentSuggestions.ToList()
                });

                // Only accept the refinement if it improved the score
                if (newScore > currentScore)
                {
                    currentResponse = refinedResponse;
                    currentScore = newScore;
                    currentSuggestions = evaluationResult.Value.Suggestions;
                    reasoningSteps.Add($"Accepted refinement, new score: {newScore:F2}");
                }
                else
                {
                    reasoningSteps.Add($"Rejected refinement (score {newScore:F2} <= {currentScore:F2})");
                }

                // Check if we've reached the target score
                if (!evaluationResult.Value.NeedsRefinement || currentScore >= request.TargetScore)
                {
                    reasoningSteps.Add($"Target score reached or refinement no longer needed");
                    break;
                }
            }

            reachedMaxIterations = iteration >= request.MaxIterations &&
                                   evaluationResult.Value.NeedsRefinement &&
                                   currentScore < request.TargetScore;

            if (reachedMaxIterations)
            {
                reasoningSteps.Add($"Reached maximum iterations ({request.MaxIterations})");
            }

            _logger.LogInformation(
                "Refinement complete - Iterations: {Iterations}, Initial: {Initial:F2}, Final: {Final:F2}",
                iteration, initialScore, currentScore);

            return Result<RefinementResult>.Success(new RefinementResult
            {
                FinalResponse = currentResponse,
                WasRefined = currentResponse != request.OriginalResponse,
                RefinementIterations = iteration,
                ReachedMaxIterations = reachedMaxIterations,
                InitialScore = initialScore,
                FinalScore = currentScore,
                HadErrors = errors.Count > 0,
                Errors = errors,
                RefinementHistory = refinementHistory,
                ReasoningSteps = reasoningSteps
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during refinement");
            return Result<RefinementResult>.Failure(
                Error.Internal("Refinement.Error", ex.Message));
        }
    }

    private EvaluationRequest CreateEvaluationRequest(RefinementRequest request, string response)
    {
        return new EvaluationRequest
        {
            Query = request.Query,
            Response = response,
            SourceContexts = request.SourceContexts,
            WorkspaceId = request.WorkspaceId
        };
    }

    private string BuildRefinementPrompt(
        string query,
        string currentResponse,
        List<string> sourceContexts,
        List<string> suggestions,
        EvaluationResult evaluation)
    {
        var sourcesText = sourceContexts.Count > 0
            ? string.Join("\n\n", sourceContexts.Select((s, i) => $"[Source {i + 1}]: {s}"))
            : "No sources available.";

        var suggestionsText = suggestions.Count > 0
            ? string.Join("\n", suggestions.Select(s => $"- {s}"))
            : "- Improve overall quality and completeness";

        var scoreInfo = $"""
            Current Quality Scores:
            - Completeness: {evaluation.CompletenessScore:F2}
            - Faithfulness: {evaluation.FaithfulnessScore:F2}
            - Relevance: {evaluation.RelevanceScore:F2}
            - Overall: {evaluation.OverallScore:F2}
            """;

        return $"""
            You are an expert at improving responses. Your task is to refine the following response to better answer the user's query.

            ORIGINAL QUERY:
            {query}

            CURRENT RESPONSE:
            {currentResponse}

            SOURCE DOCUMENTS:
            {sourcesText}

            {scoreInfo}

            IMPROVEMENT SUGGESTIONS:
            {suggestionsText}

            INSTRUCTIONS:
            1. Address all aspects of the original query
            2. Only use information from the source documents - do not add unsupported claims
            3. Apply the improvement suggestions above
            4. Maintain accuracy and avoid hallucination
            5. Be concise but complete

            IMPROVED RESPONSE:
            """;
    }
}
