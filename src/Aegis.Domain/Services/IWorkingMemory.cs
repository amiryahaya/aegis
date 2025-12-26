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
