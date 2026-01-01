using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Service for managing conversation context and multi-turn query support
/// </summary>
public interface IConversationContextService
{
    /// <summary>
    /// Builds context from session history for the next query
    /// </summary>
    Task<Result<ConversationContext>> BuildContextAsync(
        Guid sessionId,
        BuildContextOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rewrites a query using conversation context for better retrieval
    /// </summary>
    Task<Result<RewrittenQuery>> RewriteQueryAsync(
        string query,
        ConversationContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves pronouns and references in a query using conversation history
    /// </summary>
    Task<Result<string>> ResolveReferencesAsync(
        string query,
        ConversationContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts and tracks entities mentioned in the conversation
    /// </summary>
    Task<Result<EntityTracker>> TrackEntitiesAsync(
        Guid sessionId,
        string text,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current topic/focus of the conversation
    /// </summary>
    Task<Result<ConversationTopic>> GetCurrentTopicAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Detects topic shifts in the conversation
    /// </summary>
    Task<Result<TopicShift>> DetectTopicShiftAsync(
        Guid sessionId,
        string newQuery,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a summary of the conversation so far
    /// </summary>
    Task<Result<SessionConversationSummary>> SummarizeConversationAsync(
        Guid sessionId,
        SummarizeOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Compresses context when it exceeds token limits
    /// </summary>
    Task<Result<ConversationContext>> CompressContextAsync(
        ConversationContext context,
        int targetTokens,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Identifies the intent of a query in context
    /// </summary>
    Task<Result<SessionQueryIntent>> ClassifyIntentAsync(
        string query,
        ConversationContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Suggests follow-up questions based on conversation context
    /// </summary>
    Task<Result<IReadOnlyList<string>>> GenerateFollowUpsAsync(
        Guid sessionId,
        string lastResponse,
        int count = 3,
        CancellationToken cancellationToken = default);
}

#region Context Types

/// <summary>
/// Conversation context built from session history
/// </summary>
public record ConversationContext
{
    public Guid SessionId { get; init; }
    public required string SystemPrompt { get; init; }
    public List<ContextMessage> Messages { get; init; } = new();
    public List<SessionTrackedEntity> Entities { get; init; } = new();
    public ConversationTopic? CurrentTopic { get; init; }
    public string? ConversationSummary { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = new();
    public int EstimatedTokens { get; init; }
    public DateTime BuiltAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// A message in the conversation context
/// </summary>
public record ContextMessage
{
    public required ContextRole Role { get; init; }
    public required string Content { get; init; }
    public int TurnNumber { get; init; }
    public DateTime Timestamp { get; init; }
    public int EstimatedTokens { get; init; }
}

public enum ContextRole
{
    System,
    User,
    Assistant
}

/// <summary>
/// Options for building conversation context
/// </summary>
public record BuildContextOptions
{
    public int MaxTurns { get; init; } = 10;
    public int MaxTokens { get; init; } = 4000;
    public bool IncludeSystemPrompt { get; init; } = true;
    public bool IncludeSummary { get; init; } = true;
    public bool IncludeEntities { get; init; } = true;
    public bool CompressIfNeeded { get; init; } = true;
    public ContextStrategy Strategy { get; init; } = ContextStrategy.RecentFirst;
}

public enum ContextStrategy
{
    RecentFirst,
    RelevantFirst,
    Summarized,
    Hybrid
}

#endregion

#region Query Rewriting

/// <summary>
/// Query rewritten with context for better retrieval
/// </summary>
public record RewrittenQuery
{
    public required string OriginalQuery { get; init; }
    public required string RewrittenText { get; init; }
    public List<string> ExpandedTerms { get; init; } = new();
    public List<string> ResolvedReferences { get; init; } = new();
    public double ConfidenceScore { get; init; }
    public string? Explanation { get; init; }
}

#endregion

#region Entity Tracking

/// <summary>
/// Tracks entities mentioned in the conversation
/// </summary>
public record EntityTracker
{
    public Guid SessionId { get; init; }
    public List<SessionTrackedEntity> Entities { get; init; } = new();
    public Dictionary<string, List<string>> Coreferences { get; init; } = new();
    public DateTime LastUpdated { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// An entity tracked across the conversation
/// </summary>
public record SessionTrackedEntity
{
    public required string Name { get; init; }
    public required SessionEntityType Type { get; init; }
    public List<string> Aliases { get; init; } = new();
    public List<string> Mentions { get; init; } = new();
    public int MentionCount { get; init; }
    public int FirstMentionTurn { get; init; }
    public int LastMentionTurn { get; init; }
    public Dictionary<string, object> Attributes { get; init; } = new();
    public double Salience { get; init; }
}

public enum SessionEntityType
{
    Person,
    Organization,
    Location,
    Date,
    Document,
    Concept,
    Event,
    Product,
    Technology,
    Other
}

#endregion

#region Topic Management

/// <summary>
/// Current topic/focus of the conversation
/// </summary>
public record ConversationTopic
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public List<string> Keywords { get; init; } = new();
    public List<string> RelatedTopics { get; init; } = new();
    public int StartTurn { get; init; }
    public double Confidence { get; init; }
}

/// <summary>
/// Detected shift in conversation topic
/// </summary>
public record TopicShift
{
    public ConversationTopic? PreviousTopic { get; init; }
    public ConversationTopic? NewTopic { get; init; }
    public bool IsSignificantShift { get; init; }
    public double ShiftScore { get; init; }
    public string? Reason { get; init; }
}

#endregion

#region Summarization

/// <summary>
/// Summary of the conversation
/// </summary>
public record SessionConversationSummary
{
    public Guid SessionId { get; init; }
    public required string Summary { get; init; }
    public List<string> KeyPoints { get; init; } = new();
    public List<string> QuestionsAsked { get; init; } = new();
    public List<string> TopicsDiscussed { get; init; } = new();
    public List<string> UnresolvedQuestions { get; init; } = new();
    public int TurnsCovered { get; init; }
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Options for summarization
/// </summary>
public record SummarizeOptions
{
    public int MaxLength { get; init; } = 500;
    public bool IncludeKeyPoints { get; init; } = true;
    public bool IncludeQuestions { get; init; } = true;
    public bool IncludeTopics { get; init; } = true;
    public SummaryStyle Style { get; init; } = SummaryStyle.Concise;
}

public enum SummaryStyle
{
    Concise,
    Detailed,
    Bullet,
    Narrative
}

#endregion

#region Intent Classification

/// <summary>
/// Classified intent of a query
/// </summary>
public record SessionQueryIntent
{
    public required SessionIntentType Type { get; init; }
    public double Confidence { get; init; }
    public List<string> Slots { get; init; } = new();
    public Dictionary<string, string> Parameters { get; init; } = new();
    public bool RequiresContext { get; init; }
    public bool IsFollowUp { get; init; }
    public string? Explanation { get; init; }
}

public enum SessionIntentType
{
    Question,
    Clarification,
    FollowUp,
    Comparison,
    Summary,
    Definition,
    Explanation,
    List,
    Recommendation,
    Verification,
    Navigation,
    Feedback,
    Other
}

#endregion
