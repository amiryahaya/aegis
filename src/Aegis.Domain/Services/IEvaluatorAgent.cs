using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Agent responsible for evaluating response quality
/// </summary>
public interface IEvaluatorAgent : IAgent
{
    /// <summary>
    /// Evaluates a response for quality, completeness, and faithfulness
    /// </summary>
    /// <param name="request">The evaluation request containing query, response, and sources</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Evaluation result with scores and suggestions</returns>
    Task<Result<EvaluationResult>> EvaluateAsync(
        EvaluationRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Request for response evaluation
/// </summary>
public record EvaluationRequest
{
    /// <summary>
    /// The original user query
    /// </summary>
    public required string Query { get; init; }

    /// <summary>
    /// The generated response to evaluate
    /// </summary>
    public required string Response { get; init; }

    /// <summary>
    /// Source contexts used to generate the response
    /// </summary>
    public List<string> SourceContexts { get; init; } = new();

    /// <summary>
    /// Optional workspace ID for context
    /// </summary>
    public Guid? WorkspaceId { get; init; }

    /// <summary>
    /// Optional conversation history for multi-turn context
    /// </summary>
    public List<ConversationTurn> ConversationHistory { get; init; } = new();
}

/// <summary>
/// Represents a turn in a conversation
/// </summary>
public record ConversationTurn
{
    /// <summary>
    /// Role of the speaker (user or assistant)
    /// </summary>
    public required string Role { get; init; }

    /// <summary>
    /// Content of the message
    /// </summary>
    public required string Content { get; init; }
}

/// <summary>
/// Result of response evaluation
/// </summary>
public record EvaluationResult
{
    /// <summary>
    /// Score indicating how completely the response addresses the query (0.0 - 1.0)
    /// </summary>
    public double CompletenessScore { get; init; }

    /// <summary>
    /// Score indicating how faithfully the response follows the source contexts (0.0 - 1.0)
    /// </summary>
    public double FaithfulnessScore { get; init; }

    /// <summary>
    /// Score indicating response relevance to the query (0.0 - 1.0)
    /// </summary>
    public double RelevanceScore { get; init; }

    /// <summary>
    /// Overall quality score (weighted combination of other scores)
    /// </summary>
    public double OverallScore { get; init; }

    /// <summary>
    /// Confidence in the evaluation itself (0.0 - 1.0)
    /// </summary>
    public double EvaluationConfidence { get; init; }

    /// <summary>
    /// Indicates if the response contains potential hallucinations
    /// </summary>
    public bool HasPotentialHallucination { get; init; }

    /// <summary>
    /// Indicates if the response needs refinement based on evaluation
    /// </summary>
    public bool NeedsRefinement { get; init; }

    /// <summary>
    /// Threshold below which refinement is recommended
    /// </summary>
    public static double RefinementThreshold => 0.7;

    /// <summary>
    /// Suggestions for improving the response
    /// </summary>
    public List<string> Suggestions { get; init; } = new();

    /// <summary>
    /// Detailed reasoning steps for the evaluation
    /// </summary>
    public List<string> ReasoningSteps { get; init; } = new();

    /// <summary>
    /// Claims detected in the response for faithfulness checking
    /// </summary>
    public List<ClaimVerification> ClaimVerifications { get; init; } = new();
}

/// <summary>
/// Verification result for a single claim in the response
/// </summary>
public record ClaimVerification
{
    /// <summary>
    /// The claim extracted from the response
    /// </summary>
    public required string Claim { get; init; }

    /// <summary>
    /// Whether the claim is supported by sources
    /// </summary>
    public bool IsSupported { get; init; }

    /// <summary>
    /// Index of the supporting source context (if any)
    /// </summary>
    public int? SupportingSourceIndex { get; init; }

    /// <summary>
    /// Confidence in this verification (0.0 - 1.0)
    /// </summary>
    public double Confidence { get; init; }
}
