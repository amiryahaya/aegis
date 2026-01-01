using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Service for iteratively refining responses based on evaluation feedback
/// </summary>
public interface IRefinementLoop
{
    /// <summary>
    /// Refines a response through iterative evaluation and improvement
    /// </summary>
    /// <param name="request">The refinement request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The refined response with improvement metrics</returns>
    Task<Result<RefinementResult>> RefineAsync(
        RefinementRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Request for response refinement
/// </summary>
public record RefinementRequest
{
    /// <summary>
    /// The original user query
    /// </summary>
    public required string Query { get; init; }

    /// <summary>
    /// The original response to refine
    /// </summary>
    public required string OriginalResponse { get; init; }

    /// <summary>
    /// Source contexts for faithfulness checking
    /// </summary>
    public List<string> SourceContexts { get; init; } = new();

    /// <summary>
    /// Maximum number of refinement iterations
    /// </summary>
    public int MaxIterations { get; init; } = 3;

    /// <summary>
    /// Target quality score threshold (0.0 - 1.0)
    /// </summary>
    public double TargetScore { get; init; } = 0.8;

    /// <summary>
    /// Optional workspace ID for context
    /// </summary>
    public Guid? WorkspaceId { get; init; }
}

/// <summary>
/// Result of the refinement process
/// </summary>
public record RefinementResult
{
    /// <summary>
    /// The final refined response
    /// </summary>
    public required string FinalResponse { get; init; }

    /// <summary>
    /// Whether the response was refined (false if original was good enough)
    /// </summary>
    public bool WasRefined { get; init; }

    /// <summary>
    /// Number of refinement iterations performed
    /// </summary>
    public int RefinementIterations { get; init; }

    /// <summary>
    /// Whether the maximum iterations limit was reached
    /// </summary>
    public bool ReachedMaxIterations { get; init; }

    /// <summary>
    /// Initial evaluation score
    /// </summary>
    public double InitialScore { get; init; }

    /// <summary>
    /// Final evaluation score after refinement
    /// </summary>
    public double FinalScore { get; init; }

    /// <summary>
    /// Score improvement (FinalScore - InitialScore)
    /// </summary>
    public double ScoreImprovement => FinalScore - InitialScore;

    /// <summary>
    /// Whether errors occurred during refinement
    /// </summary>
    public bool HadErrors { get; init; }

    /// <summary>
    /// Error messages if any occurred
    /// </summary>
    public List<string> Errors { get; init; } = new();

    /// <summary>
    /// History of refinement attempts
    /// </summary>
    public List<RefinementAttempt> RefinementHistory { get; init; } = new();

    /// <summary>
    /// Reasoning steps taken during refinement
    /// </summary>
    public List<string> ReasoningSteps { get; init; } = new();
}

/// <summary>
/// Record of a single refinement attempt
/// </summary>
public record RefinementAttempt
{
    /// <summary>
    /// Iteration number (1-based)
    /// </summary>
    public int Iteration { get; init; }

    /// <summary>
    /// The response at this iteration
    /// </summary>
    public required string Response { get; init; }

    /// <summary>
    /// Evaluation score at this iteration
    /// </summary>
    public double EvaluationScore { get; init; }

    /// <summary>
    /// Suggestions used for this refinement
    /// </summary>
    public List<string> SuggestionsUsed { get; init; } = new();

    /// <summary>
    /// Timestamp of the attempt
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
