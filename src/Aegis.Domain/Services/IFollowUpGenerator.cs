using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Service for generating suggested follow-up questions
/// </summary>
public interface IFollowUpGenerator
{
    /// <summary>
    /// Generate follow-up questions based on the conversation context
    /// </summary>
    /// <param name="request">The follow-up generation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Generated follow-up questions</returns>
    Task<Result<FollowUpResult>> GenerateAsync(
        FollowUpRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate follow-up questions using rule-based approach (no LLM)
    /// </summary>
    /// <param name="request">The follow-up generation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Generated follow-up questions</returns>
    Task<Result<FollowUpResult>> GenerateRuleBasedAsync(
        FollowUpRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Request for generating follow-up questions
/// </summary>
public record FollowUpRequest
{
    /// <summary>
    /// The original user query
    /// </summary>
    public required string OriginalQuery { get; init; }

    /// <summary>
    /// The assistant's response
    /// </summary>
    public required string Response { get; init; }

    /// <summary>
    /// Entities tracked in the conversation
    /// </summary>
    public List<string> TrackedEntities { get; init; } = new();

    /// <summary>
    /// Previous conversation history
    /// </summary>
    public List<string> ConversationHistory { get; init; } = new();

    /// <summary>
    /// Current conversation topic
    /// </summary>
    public string? CurrentTopic { get; init; }

    /// <summary>
    /// Maximum number of questions to generate
    /// </summary>
    public int MaxQuestions { get; init; } = 3;

    /// <summary>
    /// Optional workspace ID for context
    /// </summary>
    public Guid? WorkspaceId { get; init; }
}

/// <summary>
/// Result of follow-up question generation
/// </summary>
public record FollowUpResult
{
    /// <summary>
    /// Generated follow-up questions
    /// </summary>
    public List<string> FollowUpQuestions { get; init; } = new();

    /// <summary>
    /// Questions categorized by type
    /// </summary>
    public Dictionary<string, List<string>> CategorizedQuestions { get; init; } = new();

    /// <summary>
    /// Method used for generation (llm or rule-based)
    /// </summary>
    public string GenerationMethod { get; init; } = "llm";

    /// <summary>
    /// Confidence in the generated questions (0.0 - 1.0)
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// Reasoning for the generated questions
    /// </summary>
    public List<string> ReasoningSteps { get; init; } = new();
}
