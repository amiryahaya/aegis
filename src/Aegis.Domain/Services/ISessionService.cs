using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Service for managing user sessions and conversation history
/// </summary>
public interface ISessionService
{
    /// <summary>
    /// Creates a new session for a user
    /// </summary>
    Task<Result<Session>> CreateAsync(CreateSessionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a session by ID
    /// </summary>
    Task<Result<Session>> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all sessions for a user with optional filtering
    /// </summary>
    Task<Result<SessionPage>> GetForUserAsync(Guid userId, SessionFilter? filter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets active sessions for a user
    /// </summary>
    Task<Result<IReadOnlyList<Session>>> GetActiveSessionsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a conversation turn to a session
    /// </summary>
    Task<Result<SessionTurn>> AddTurnAsync(Guid sessionId, AddTurnRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets conversation history for a session
    /// </summary>
    Task<Result<IReadOnlyList<SessionTurn>>> GetConversationAsync(Guid sessionId, int? lastNTurns = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates session metadata
    /// </summary>
    Task<Result<Session>> UpdateAsync(Guid sessionId, UpdateSessionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates session title (auto-generated from first query or manual)
    /// </summary>
    Task<Result> UpdateTitleAsync(Guid sessionId, string title, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ends/closes a session
    /// </summary>
    Task<Result> EndSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a session and all its conversation history
    /// </summary>
    Task<Result> DeleteAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes sessions older than the specified date
    /// </summary>
    Task<Result<int>> DeleteOlderThanAsync(DateTime before, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets session statistics for a user
    /// </summary>
    Task<Result<SessionStats>> GetStatsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Shares a session with another user or makes it public
    /// </summary>
    Task<Result<SessionShare>> ShareAsync(Guid sessionId, ShareSessionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets shared sessions accessible to a user
    /// </summary>
    Task<Result<IReadOnlyList<Session>>> GetSharedWithUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports session conversation to various formats
    /// </summary>
    Task<Result<SessionExport>> ExportAsync(Guid sessionId, SessionExportFormat format, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds feedback to a specific turn in the conversation
    /// </summary>
    Task<Result> AddTurnFeedbackAsync(Guid sessionId, Guid turnId, TurnFeedback feedback, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recent sessions across all users (admin)
    /// </summary>
    Task<Result<SessionPage>> GetRecentAsync(SessionFilter? filter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches sessions by content
    /// </summary>
    Task<Result<SessionPage>> SearchAsync(string query, Guid? userId = null, SessionFilter? filter = null, CancellationToken cancellationToken = default);
}

#region Session Entity

/// <summary>
/// Represents a user session containing conversation history
/// </summary>
public record Session
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid UserId { get; init; }
    public Guid? TeamId { get; init; }
    public Guid? WorkspaceId { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public SessionStatus Status { get; init; } = SessionStatus.Active;
    public SessionType Type { get; init; } = SessionType.Query;
    public List<SessionTurn> Turns { get; init; } = new();
    public Dictionary<string, object> Metadata { get; init; } = new();
    public List<string> Tags { get; init; } = new();
    public SessionSettings Settings { get; init; } = new();
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? LastActivityAt { get; init; }
    public DateTime? EndedAt { get; init; }
    public int TurnCount => Turns.Count;
    public TimeSpan? Duration => EndedAt.HasValue ? EndedAt.Value - CreatedAt : DateTime.UtcNow - CreatedAt;
}

/// <summary>
/// Represents a single turn in a conversation (query + response)
/// </summary>
public record SessionTurn
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid SessionId { get; init; }
    public int TurnNumber { get; init; }
    public required string UserQuery { get; init; }
    public string? SystemResponse { get; init; }
    public TurnStatus Status { get; init; } = TurnStatus.Pending;
    public List<SessionSourceReference> Sources { get; init; } = new();
    public List<string> FollowUpQuestions { get; init; } = new();
    public TurnMetrics? Metrics { get; init; }
    public TurnFeedback? Feedback { get; init; }
    public Dictionary<string, object> Context { get; init; } = new();
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; init; }
    public TimeSpan? ProcessingTime => CompletedAt.HasValue ? CompletedAt.Value - CreatedAt : null;
}

/// <summary>
/// Reference to a source document used in a response
/// </summary>
public record SessionSourceReference
{
    public Guid DocumentId { get; init; }
    public required string DocumentName { get; init; }
    public string? ChunkId { get; init; }
    public string? Excerpt { get; init; }
    public double RelevanceScore { get; init; }
    public int? PageNumber { get; init; }
    public string? Section { get; init; }
}

/// <summary>
/// Metrics for a conversation turn
/// </summary>
public record TurnMetrics
{
    public TimeSpan RetrievalTime { get; init; }
    public TimeSpan GenerationTime { get; init; }
    public TimeSpan TotalTime { get; init; }
    public int SourcesRetrieved { get; init; }
    public int SourcesUsed { get; init; }
    public int TokensUsed { get; init; }
    public double? RelevanceScore { get; init; }
    public double? FaithfulnessScore { get; init; }
    public double? CompletenessScore { get; init; }
    public bool? CacheHit { get; init; }
}

/// <summary>
/// User feedback for a conversation turn
/// </summary>
public record TurnFeedback
{
    public FeedbackRating Rating { get; init; }
    public string? Comment { get; init; }
    public List<string> Issues { get; init; } = new();
    public DateTime ProvidedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Session-level settings
/// </summary>
public record SessionSettings
{
    public bool EnableFollowUps { get; init; } = true;
    public bool EnableCitations { get; init; } = true;
    public bool EnableStreaming { get; init; } = true;
    public int MaxTurns { get; init; } = 100;
    public int ContextWindowTurns { get; init; } = 10;
    public string? PreferredModel { get; init; }
    public string? SystemPrompt { get; init; }
    public double? Temperature { get; init; }
    public List<Guid>? RestrictToDocuments { get; init; }
    public List<Guid>? RestrictToDataSources { get; init; }
}

#endregion

#region Enums

public enum SessionStatus
{
    Active,
    Paused,
    Ended,
    Archived,
    Deleted
}

public enum SessionType
{
    Query,
    Research,
    Analysis,
    Comparison,
    Summary,
    Custom
}

public enum TurnStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    Cancelled
}

public enum FeedbackRating
{
    Positive,
    Negative,
    Neutral
}

public enum SessionExportFormat
{
    Json,
    Markdown,
    Html,
    Pdf,
    Text
}

public enum SessionSortBy
{
    CreatedAt,
    LastActivity,
    TurnCount,
    Title
}

#endregion

#region Request/Response DTOs

public record CreateSessionRequest
{
    public required Guid UserId { get; init; }
    public Guid? TeamId { get; init; }
    public Guid? WorkspaceId { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public SessionType Type { get; init; } = SessionType.Query;
    public List<string>? Tags { get; init; }
    public SessionSettings? Settings { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}

public record UpdateSessionRequest
{
    public string? Title { get; init; }
    public string? Description { get; init; }
    public List<string>? Tags { get; init; }
    public SessionSettings? Settings { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}

public record AddTurnRequest
{
    public required string Query { get; init; }
    public Dictionary<string, object>? Context { get; init; }
}

public record ShareSessionRequest
{
    public Guid? ShareWithUserId { get; init; }
    public Guid? ShareWithTeamId { get; init; }
    public bool IsPublic { get; init; }
    public SessionSharePermission Permission { get; init; } = SessionSharePermission.ReadOnly;
    public DateTime? ExpiresAt { get; init; }
}

public record SessionShare
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid SessionId { get; init; }
    public Guid SharedByUserId { get; init; }
    public Guid? SharedWithUserId { get; init; }
    public Guid? SharedWithTeamId { get; init; }
    public bool IsPublic { get; init; }
    public SessionSharePermission Permission { get; init; }
    public string? ShareLink { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; init; }
}

public enum SessionSharePermission
{
    ReadOnly,
    Comment,
    Collaborate
}

public record SessionFilter
{
    public SessionStatus? Status { get; init; }
    public SessionType? Type { get; init; }
    public List<string>? Tags { get; init; }
    public DateTime? Since { get; init; }
    public DateTime? Until { get; init; }
    public Guid? TeamId { get; init; }
    public Guid? WorkspaceId { get; init; }
    public SessionSortBy SortBy { get; init; } = SessionSortBy.LastActivity;
    public bool SortDescending { get; init; } = true;
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public record SessionPage
{
    public IReadOnlyList<Session> Items { get; init; } = Array.Empty<Session>();
    public int TotalCount { get; init; }
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => PageNumber < TotalPages;
    public bool HasPreviousPage => PageNumber > 1;
}

public record SessionStats
{
    public Guid UserId { get; init; }
    public int TotalSessions { get; init; }
    public int ActiveSessions { get; init; }
    public int TotalTurns { get; init; }
    public int TotalQueriesThisWeek { get; init; }
    public int TotalQueriesThisMonth { get; init; }
    public double AverageSessionDuration { get; init; }
    public double AverageTurnsPerSession { get; init; }
    public Dictionary<SessionType, int> SessionsByType { get; init; } = new();
    public Dictionary<FeedbackRating, int> FeedbackByRating { get; init; } = new();
    public DateTime? LastSessionAt { get; init; }
    public List<string> TopTags { get; init; } = new();
}

public record SessionExport
{
    public Guid SessionId { get; init; }
    public SessionExportFormat Format { get; init; }
    public required string Content { get; init; }
    public string? FileName { get; init; }
    public string? ContentType { get; init; }
    public DateTime ExportedAt { get; init; } = DateTime.UtcNow;
}

#endregion

#region Session Templates

/// <summary>
/// Pre-defined session templates for common use cases
/// </summary>
public static class SessionTemplates
{
    public static CreateSessionRequest QuickQuery(Guid userId, string? title = null) => new()
    {
        UserId = userId,
        Title = title ?? "Quick Query",
        Type = SessionType.Query,
        Settings = new SessionSettings
        {
            MaxTurns = 10,
            ContextWindowTurns = 3,
            EnableFollowUps = true
        }
    };

    public static CreateSessionRequest ResearchSession(Guid userId, string topic, Guid? teamId = null) => new()
    {
        UserId = userId,
        TeamId = teamId,
        Title = $"Research: {topic}",
        Type = SessionType.Research,
        Tags = new List<string> { "research", topic.ToLowerInvariant() },
        Settings = new SessionSettings
        {
            MaxTurns = 100,
            ContextWindowTurns = 20,
            EnableFollowUps = true,
            EnableCitations = true
        }
    };

    public static CreateSessionRequest AnalysisSession(Guid userId, string subject, List<Guid>? documentIds = null) => new()
    {
        UserId = userId,
        Title = $"Analysis: {subject}",
        Type = SessionType.Analysis,
        Tags = new List<string> { "analysis" },
        Settings = new SessionSettings
        {
            MaxTurns = 50,
            ContextWindowTurns = 15,
            EnableCitations = true,
            RestrictToDocuments = documentIds
        }
    };

    public static CreateSessionRequest ComparisonSession(Guid userId, string subject) => new()
    {
        UserId = userId,
        Title = $"Comparison: {subject}",
        Type = SessionType.Comparison,
        Tags = new List<string> { "comparison" },
        Settings = new SessionSettings
        {
            MaxTurns = 30,
            ContextWindowTurns = 10,
            EnableCitations = true
        }
    };

    public static CreateSessionRequest SummarySession(Guid userId, string documentName, Guid documentId) => new()
    {
        UserId = userId,
        Title = $"Summary: {documentName}",
        Type = SessionType.Summary,
        Tags = new List<string> { "summary" },
        Settings = new SessionSettings
        {
            MaxTurns = 20,
            ContextWindowTurns = 5,
            EnableCitations = true,
            RestrictToDocuments = new List<Guid> { documentId }
        }
    };
}

#endregion
