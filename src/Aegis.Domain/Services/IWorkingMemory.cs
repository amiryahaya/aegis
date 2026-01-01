using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Working memory service for maintaining conversation context
/// </summary>
public interface IWorkingMemory
{
    /// <summary>
    /// Store a value in working memory
    /// </summary>
    Task<Result<bool>> SetAsync(
        string sessionId,
        string key,
        object value,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieve a value from working memory
    /// </summary>
    Task<Result<T?>> GetAsync<T>(
        string sessionId,
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all context for a session
    /// </summary>
    Task<Result<Dictionary<string, object>>> GetContextAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear all memory for a session
    /// </summary>
    Task<Result<bool>> ClearAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Add a message to conversation history
    /// </summary>
    Task<Result<bool>> AddMessageAsync(
        string sessionId,
        ConversationMessage message,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get conversation history
    /// </summary>
    Task<Result<List<ConversationMessage>>> GetHistoryAsync(
        string sessionId,
        int limit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Track an entity mentioned in the conversation
    /// </summary>
    Task<Result<bool>> TrackEntityAsync(
        string sessionId,
        TrackedEntity entity,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all tracked entities for a session
    /// </summary>
    Task<Result<List<TrackedEntity>>> GetTrackedEntitiesAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolve a reference (pronoun) to a tracked entity
    /// </summary>
    Task<Result<TrackedEntity?>> ResolveReferenceAsync(
        string sessionId,
        string reference,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update the last mentioned time for an entity
    /// </summary>
    Task<Result<bool>> UpdateEntityMentionAsync(
        string sessionId,
        string entityName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Set the current conversation topic
    /// </summary>
    Task<Result<bool>> SetCurrentTopicAsync(
        string sessionId,
        string topic,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the current conversation topic
    /// </summary>
    Task<Result<string?>> GetCurrentTopicAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a summary of the conversation
    /// </summary>
    Task<Result<ConversationSummary>> GetConversationSummaryAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get formatted context window for LLM input
    /// </summary>
    Task<Result<string>> GetContextWindowAsync(
        string sessionId,
        int maxMessages = 10,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A message in the conversation
/// </summary>
public record ConversationMessage
{
    /// <summary>
    /// Message ID
    /// </summary>
    public required Guid MessageId { get; init; }

    /// <summary>
    /// Role (user, assistant, system)
    /// </summary>
    public required string Role { get; init; }

    /// <summary>
    /// Message content
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Timestamp
    /// </summary>
    public required DateTime Timestamp { get; init; }

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();
}

/// <summary>
/// An entity tracked across the conversation
/// </summary>
public record TrackedEntity
{
    /// <summary>
    /// Entity name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Entity type (Person, Organization, Location, etc.)
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Known aliases for this entity
    /// </summary>
    public List<string> Aliases { get; init; } = new();

    /// <summary>
    /// When this entity was first mentioned
    /// </summary>
    public DateTime FirstMentionedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// When this entity was last mentioned
    /// </summary>
    public DateTime LastMentionedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Number of times mentioned
    /// </summary>
    public int MentionCount { get; set; } = 1;

    /// <summary>
    /// Additional attributes
    /// </summary>
    public Dictionary<string, object> Attributes { get; init; } = new();
}

/// <summary>
/// Summary of a conversation
/// </summary>
public record ConversationSummary
{
    /// <summary>
    /// Total number of messages
    /// </summary>
    public int TotalMessages { get; init; }

    /// <summary>
    /// Number of user messages
    /// </summary>
    public int UserMessages { get; init; }

    /// <summary>
    /// Number of assistant messages
    /// </summary>
    public int AssistantMessages { get; init; }

    /// <summary>
    /// Number of system messages
    /// </summary>
    public int SystemMessages { get; init; }

    /// <summary>
    /// Current topic (if set)
    /// </summary>
    public string? CurrentTopic { get; init; }

    /// <summary>
    /// Entities mentioned in the conversation
    /// </summary>
    public List<string> TrackedEntityNames { get; init; } = new();

    /// <summary>
    /// When the conversation started
    /// </summary>
    public DateTime? StartedAt { get; init; }

    /// <summary>
    /// When the last message was sent
    /// </summary>
    public DateTime? LastMessageAt { get; init; }

    /// <summary>
    /// Estimated token count
    /// </summary>
    public int EstimatedTokenCount { get; init; }
}
