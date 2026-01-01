using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Service for managing comments and threaded discussions
/// </summary>
public interface ICommentService
{
    /// <summary>
    /// Creates a new comment
    /// </summary>
    Task<Result<Comment>> CreateCommentAsync(
        CreateCommentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a comment
    /// </summary>
    Task<Result<Comment>> UpdateCommentAsync(
        Guid commentId,
        UpdateCommentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a comment
    /// </summary>
    Task<Result> DeleteCommentAsync(
        Guid commentId,
        Guid deletedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a comment by ID
    /// </summary>
    Task<Result<Comment>> GetCommentByIdAsync(
        Guid commentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all comments for a resource
    /// </summary>
    Task<Result<CommentPage>> GetCommentsForResourceAsync(
        Guid resourceId,
        CommentableResourceType resourceType,
        CommentFilter? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets replies to a comment
    /// </summary>
    Task<Result<IReadOnlyList<Comment>>> GetRepliesAsync(
        Guid parentCommentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a reaction to a comment
    /// </summary>
    Task<Result<CommentReaction>> AddReactionAsync(
        Guid commentId,
        AddReactionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a reaction from a comment
    /// </summary>
    Task<Result> RemoveReactionAsync(
        Guid commentId,
        Guid userId,
        ReactionType reactionType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets reactions for a comment
    /// </summary>
    Task<Result<IReadOnlyList<CommentReaction>>> GetReactionsAsync(
        Guid commentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a comment thread
    /// </summary>
    Task<Result<Comment>> ResolveCommentAsync(
        Guid commentId,
        Guid resolvedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reopens a resolved comment thread
    /// </summary>
    Task<Result<Comment>> ReopenCommentAsync(
        Guid commentId,
        Guid reopenedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pins a comment to the top
    /// </summary>
    Task<Result<Comment>> PinCommentAsync(
        Guid commentId,
        Guid pinnedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Unpins a comment
    /// </summary>
    Task<Result<Comment>> UnpinCommentAsync(
        Guid commentId,
        Guid unpinnedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all @mentions for a user
    /// </summary>
    Task<Result<MentionPage>> GetMentionsForUserAsync(
        Guid userId,
        MentionFilter? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks mentions as read
    /// </summary>
    Task<Result> MarkMentionsAsReadAsync(
        Guid userId,
        IEnumerable<Guid>? commentIds = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches comments
    /// </summary>
    Task<Result<CommentPage>> SearchCommentsAsync(
        CommentSearchRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets comment statistics
    /// </summary>
    Task<Result<CommentStats>> GetStatsAsync(
        Guid? resourceId = null,
        CommentableResourceType? resourceType = null,
        CancellationToken cancellationToken = default);
}

#region Comment Types

/// <summary>
/// A comment on a resource
/// </summary>
public record Comment
{
    public Guid Id { get; init; }
    public Guid ResourceId { get; init; }
    public required CommentableResourceType ResourceType { get; init; }
    public Guid AuthorId { get; init; }
    public required string AuthorName { get; init; }
    public string? AuthorAvatarUrl { get; init; }
    public required string Content { get; init; }
    public string? HtmlContent { get; init; }
    public Guid? ParentCommentId { get; init; }
    public Guid? ThreadRootId { get; init; }
    public CommentStatus Status { get; init; } = CommentStatus.Active;
    public bool IsResolved { get; init; }
    public Guid? ResolvedBy { get; init; }
    public DateTime? ResolvedAt { get; init; }
    public bool IsPinned { get; init; }
    public Guid? PinnedBy { get; init; }
    public DateTime? PinnedAt { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? EditedAt { get; init; }
    public bool IsEdited { get; init; }
    public List<CommentMention> Mentions { get; init; } = new();
    public List<CommentAttachment> Attachments { get; init; } = new();
    public CommentAnchor? Anchor { get; init; }
    public Dictionary<ReactionType, int> ReactionCounts { get; init; } = new();
    public int ReplyCount { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = new();
}

public enum CommentableResourceType
{
    Session,
    Document,
    Query,
    QueryResult,
    Report,
    Dashboard,
    DataSource,
    Workspace,
    Collection
}

public enum CommentStatus
{
    Active,
    Deleted,
    Hidden,
    Flagged
}

/// <summary>
/// Request to create a comment
/// </summary>
public record CreateCommentRequest
{
    public Guid ResourceId { get; init; }
    public required CommentableResourceType ResourceType { get; init; }
    public Guid AuthorId { get; init; }
    public required string AuthorName { get; init; }
    public string? AuthorAvatarUrl { get; init; }
    public required string Content { get; init; }
    public Guid? ParentCommentId { get; init; }
    public CommentAnchor? Anchor { get; init; }
    public List<Guid>? MentionedUserIds { get; init; }
    public List<CommentAttachment>? Attachments { get; init; }
}

/// <summary>
/// Request to update a comment
/// </summary>
public record UpdateCommentRequest
{
    public required string Content { get; init; }
    public Guid UpdatedBy { get; init; }
    public List<Guid>? MentionedUserIds { get; init; }
}

/// <summary>
/// Filter for listing comments
/// </summary>
public record CommentFilter
{
    public bool? IncludeReplies { get; init; } = true;
    public bool? OnlyResolved { get; init; }
    public bool? OnlyUnresolved { get; init; }
    public bool? OnlyPinned { get; init; }
    public Guid? AuthorId { get; init; }
    public DateTime? Since { get; init; }
    public CommentSortOrder SortOrder { get; init; } = CommentSortOrder.Newest;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public enum CommentSortOrder
{
    Newest,
    Oldest,
    MostReplies,
    MostReactions
}

/// <summary>
/// Paginated comments result
/// </summary>
public record CommentPage
{
    public IReadOnlyList<Comment> Comments { get; init; } = Array.Empty<Comment>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public bool HasMore => Page * PageSize < TotalCount;
}

#endregion

#region Mention Types

/// <summary>
/// A mention of a user in a comment
/// </summary>
public record CommentMention
{
    public Guid UserId { get; init; }
    public required string DisplayName { get; init; }
    public int StartPosition { get; init; }
    public int EndPosition { get; init; }
    public bool IsRead { get; init; }
    public DateTime? ReadAt { get; init; }
}

/// <summary>
/// Filter for listing mentions
/// </summary>
public record MentionFilter
{
    public bool? OnlyUnread { get; init; }
    public CommentableResourceType? ResourceType { get; init; }
    public DateTime? Since { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

/// <summary>
/// Paginated mentions result
/// </summary>
public record MentionPage
{
    public IReadOnlyList<MentionInfo> Mentions { get; init; } = Array.Empty<MentionInfo>();
    public int TotalCount { get; init; }
    public int UnreadCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

/// <summary>
/// Information about a mention
/// </summary>
public record MentionInfo
{
    public Guid CommentId { get; init; }
    public Guid ResourceId { get; init; }
    public CommentableResourceType ResourceType { get; init; }
    public Guid MentionedBy { get; init; }
    public required string MentionedByName { get; init; }
    public required string CommentContent { get; init; }
    public DateTime MentionedAt { get; init; }
    public bool IsRead { get; init; }
}

#endregion

#region Reaction Types

/// <summary>
/// A reaction to a comment
/// </summary>
public record CommentReaction
{
    public Guid CommentId { get; init; }
    public Guid UserId { get; init; }
    public required string UserName { get; init; }
    public ReactionType Type { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

public enum ReactionType
{
    Like,
    Love,
    Laugh,
    Celebrate,
    Insightful,
    Question,
    Agree,
    Disagree
}

/// <summary>
/// Request to add a reaction
/// </summary>
public record AddReactionRequest
{
    public Guid UserId { get; init; }
    public required string UserName { get; init; }
    public ReactionType Type { get; init; }
}

#endregion

#region Anchor Types

/// <summary>
/// Anchor for a comment to specific content
/// </summary>
public record CommentAnchor
{
    public AnchorType Type { get; init; }
    public string? ElementId { get; init; }
    public int? StartOffset { get; init; }
    public int? EndOffset { get; init; }
    public string? SelectedText { get; init; }
    public int? PageNumber { get; init; }
    public double? X { get; init; }
    public double? Y { get; init; }
}

public enum AnchorType
{
    Text,
    Element,
    Page,
    Point,
    Range
}

#endregion

#region Attachment Types

/// <summary>
/// An attachment to a comment
/// </summary>
public record CommentAttachment
{
    public Guid Id { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public long Size { get; init; }
    public required string Url { get; init; }
    public string? ThumbnailUrl { get; init; }
}

#endregion

#region Search Types

/// <summary>
/// Request to search comments
/// </summary>
public record CommentSearchRequest
{
    public required string Query { get; init; }
    public Guid? WorkspaceId { get; init; }
    public CommentableResourceType? ResourceType { get; init; }
    public Guid? AuthorId { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

#endregion

#region Statistics

/// <summary>
/// Comment statistics
/// </summary>
public record CommentStats
{
    public int TotalComments { get; init; }
    public int TotalThreads { get; init; }
    public int TotalReplies { get; init; }
    public int ResolvedThreads { get; init; }
    public int UnresolvedThreads { get; init; }
    public int TotalReactions { get; init; }
    public int TotalMentions { get; init; }
    public int UnreadMentions { get; init; }
    public Dictionary<CommentableResourceType, int> CommentsByResourceType { get; init; } = new();
    public Dictionary<ReactionType, int> ReactionsByType { get; init; } = new();
    public List<TopCommenter> TopCommenters { get; init; } = new();
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Top commenter information
/// </summary>
public record TopCommenter
{
    public Guid UserId { get; init; }
    public required string DisplayName { get; init; }
    public int CommentCount { get; init; }
    public int ReactionCount { get; init; }
}

#endregion
