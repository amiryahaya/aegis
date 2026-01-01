using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Service for managing activity feeds and collaboration audit trails
/// </summary>
public interface IActivityFeedService
{
    /// <summary>
    /// Records an activity
    /// </summary>
    Task<Result<Activity>> RecordActivityAsync(
        RecordActivityRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets activities for a workspace
    /// </summary>
    Task<Result<ActivityPage>> GetWorkspaceActivitiesAsync(
        Guid workspaceId,
        ActivityFilter? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets activities for a specific resource
    /// </summary>
    Task<Result<ActivityPage>> GetResourceActivitiesAsync(
        Guid resourceId,
        ActivityResourceType resourceType,
        ActivityFilter? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets activities by a specific user
    /// </summary>
    Task<Result<ActivityPage>> GetUserActivitiesAsync(
        Guid userId,
        ActivityFilter? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets personalized activity feed for a user
    /// </summary>
    Task<Result<ActivityPage>> GetPersonalizedFeedAsync(
        Guid userId,
        PersonalizedFeedOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an activity by ID
    /// </summary>
    Task<Result<Activity>> GetActivityByIdAsync(
        Guid activityId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks activities as seen by user
    /// </summary>
    Task<Result> MarkAsSeenAsync(
        Guid userId,
        IEnumerable<Guid>? activityIds = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets unseen activity count for user
    /// </summary>
    Task<Result<int>> GetUnseenCountAsync(
        Guid userId,
        Guid? workspaceId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to activity updates for a resource
    /// </summary>
    Task<Result<ActivitySubscription>> SubscribeAsync(
        SubscribeToActivityRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribes from activity updates
    /// </summary>
    Task<Result> UnsubscribeAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets user's subscriptions
    /// </summary>
    Task<Result<IReadOnlyList<ActivitySubscription>>> GetSubscriptionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Aggregates related activities into a summary
    /// </summary>
    Task<Result<IReadOnlyList<ActivityGroup>>> GetAggregatedActivitiesAsync(
        Guid workspaceId,
        AggregationOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes old activities based on retention policy
    /// </summary>
    Task<Result<int>> PurgeOldActivitiesAsync(
        TimeSpan retention,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets activity statistics
    /// </summary>
    Task<Result<ActivityStats>> GetStatsAsync(
        Guid? workspaceId = null,
        DateTime? since = null,
        CancellationToken cancellationToken = default);
}

#region Activity Types

/// <summary>
/// An activity in the feed
/// </summary>
public record Activity
{
    public Guid Id { get; init; }
    public Guid ActorId { get; init; }
    public required string ActorName { get; init; }
    public string? ActorAvatarUrl { get; init; }
    public required FeedActivityType Type { get; init; }
    public required ActivityVerb Verb { get; init; }
    public Guid? ResourceId { get; init; }
    public ActivityResourceType? ResourceType { get; init; }
    public string? ResourceName { get; init; }
    public Guid? TargetId { get; init; }
    public ActivityResourceType? TargetType { get; init; }
    public string? TargetName { get; init; }
    public Guid? WorkspaceId { get; init; }
    public string? WorkspaceName { get; init; }
    public required string Description { get; init; }
    public string? HtmlDescription { get; init; }
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public ActivityVisibility Visibility { get; init; } = ActivityVisibility.Team;
    public ActivityImportance Importance { get; init; } = ActivityImportance.Normal;
    public Dictionary<string, object> Details { get; init; } = new();
    public List<Guid> RelatedUserIds { get; init; } = new();
    public Dictionary<string, object> Metadata { get; init; } = new();
}

public enum FeedActivityType
{
    Resource,
    Collaboration,
    Comment,
    Query,
    System,
    User,
    Security
}

public enum ActivityVerb
{
    Created,
    Updated,
    Deleted,
    Shared,
    Unshared,
    Commented,
    Replied,
    Resolved,
    Reopened,
    Mentioned,
    Reacted,
    Viewed,
    Downloaded,
    Exported,
    Uploaded,
    Joined,
    Left,
    Invited,
    Removed,
    Queried,
    Answered,
    Pinned,
    Unpinned,
    Archived,
    Restored,
    Transferred
}

public enum ActivityResourceType
{
    Session,
    Document,
    Query,
    Report,
    Dashboard,
    DataSource,
    Workspace,
    Collection,
    Comment,
    User,
    Team,
    Share
}

public enum ActivityVisibility
{
    Private,
    Team,
    Workspace,
    Public
}

public enum ActivityImportance
{
    Low,
    Normal,
    High,
    Critical
}

/// <summary>
/// Request to record an activity
/// </summary>
public record RecordActivityRequest
{
    public Guid ActorId { get; init; }
    public required string ActorName { get; init; }
    public string? ActorAvatarUrl { get; init; }
    public required FeedActivityType Type { get; init; }
    public required ActivityVerb Verb { get; init; }
    public Guid? ResourceId { get; init; }
    public ActivityResourceType? ResourceType { get; init; }
    public string? ResourceName { get; init; }
    public Guid? TargetId { get; init; }
    public ActivityResourceType? TargetType { get; init; }
    public string? TargetName { get; init; }
    public Guid? WorkspaceId { get; init; }
    public string? WorkspaceName { get; init; }
    public ActivityVisibility Visibility { get; init; } = ActivityVisibility.Team;
    public ActivityImportance Importance { get; init; } = ActivityImportance.Normal;
    public Dictionary<string, object>? Details { get; init; }
    public List<Guid>? RelatedUserIds { get; init; }
}

/// <summary>
/// Filter for activities
/// </summary>
public record ActivityFilter
{
    public FeedActivityType? Type { get; init; }
    public ActivityVerb? Verb { get; init; }
    public ActivityResourceType? ResourceType { get; init; }
    public Guid? ActorId { get; init; }
    public ActivityImportance? MinImportance { get; init; }
    public DateTime? Since { get; init; }
    public DateTime? Until { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

/// <summary>
/// Paginated activities result
/// </summary>
public record ActivityPage
{
    public IReadOnlyList<Activity> Activities { get; init; } = Array.Empty<Activity>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public bool HasMore => Page * PageSize < TotalCount;
}

/// <summary>
/// Options for personalized feed
/// </summary>
public record PersonalizedFeedOptions
{
    public bool IncludeOwnActivities { get; init; } = false;
    public bool OnlySubscribed { get; init; } = false;
    public bool OnlyUnseen { get; init; } = false;
    public List<Guid>? WorkspaceIds { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

#endregion

#region Subscription Types

/// <summary>
/// A subscription to activity updates
/// </summary>
public record ActivitySubscription
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public Guid? ResourceId { get; init; }
    public ActivityResourceType? ResourceType { get; init; }
    public Guid? WorkspaceId { get; init; }
    public SubscriptionScope Scope { get; init; }
    public List<ActivityVerb> IncludedVerbs { get; init; } = new();
    public List<ActivityVerb> ExcludedVerbs { get; init; } = new();
    public bool NotifyEmail { get; init; }
    public bool NotifyInApp { get; init; } = true;
    public bool NotifyRealTime { get; init; } = true;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

public enum SubscriptionScope
{
    Resource,
    Workspace,
    User,
    All
}

/// <summary>
/// Request to subscribe to activities
/// </summary>
public record SubscribeToActivityRequest
{
    public Guid UserId { get; init; }
    public Guid? ResourceId { get; init; }
    public ActivityResourceType? ResourceType { get; init; }
    public Guid? WorkspaceId { get; init; }
    public SubscriptionScope Scope { get; init; } = SubscriptionScope.Resource;
    public List<ActivityVerb>? IncludedVerbs { get; init; }
    public List<ActivityVerb>? ExcludedVerbs { get; init; }
    public bool NotifyEmail { get; init; }
    public bool NotifyInApp { get; init; } = true;
    public bool NotifyRealTime { get; init; } = true;
}

#endregion

#region Aggregation Types

/// <summary>
/// Options for aggregating activities
/// </summary>
public record AggregationOptions
{
    public TimeSpan GroupWindow { get; init; } = TimeSpan.FromHours(1);
    public bool GroupByActor { get; init; } = true;
    public bool GroupByResource { get; init; } = true;
    public bool GroupByVerb { get; init; } = true;
    public DateTime? Since { get; init; }
    public int MaxGroups { get; init; } = 50;
}

/// <summary>
/// A group of related activities
/// </summary>
public record ActivityGroup
{
    public required string GroupKey { get; init; }
    public Guid? ActorId { get; init; }
    public string? ActorName { get; init; }
    public Guid? ResourceId { get; init; }
    public ActivityResourceType? ResourceType { get; init; }
    public string? ResourceName { get; init; }
    public ActivityVerb? Verb { get; init; }
    public int Count { get; init; }
    public DateTime FirstOccurrence { get; init; }
    public DateTime LastOccurrence { get; init; }
    public required string Summary { get; init; }
    public List<Activity> SampleActivities { get; init; } = new();
}

#endregion

#region Statistics

/// <summary>
/// Activity statistics
/// </summary>
public record ActivityStats
{
    public int TotalActivities { get; init; }
    public int ActivitiesToday { get; init; }
    public int ActivitiesThisWeek { get; init; }
    public int ActiveUsers { get; init; }
    public Dictionary<FeedActivityType, int> ByType { get; init; } = new();
    public Dictionary<ActivityVerb, int> ByVerb { get; init; } = new();
    public Dictionary<ActivityResourceType, int> ByResourceType { get; init; } = new();
    public List<ActivityTrend> Trends { get; init; } = new();
    public List<TopActivityUser> TopUsers { get; init; } = new();
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Activity trend data point
/// </summary>
public record ActivityTrend
{
    public DateTime Date { get; init; }
    public int Count { get; init; }
}

/// <summary>
/// Top activity user
/// </summary>
public record TopActivityUser
{
    public Guid UserId { get; init; }
    public required string DisplayName { get; init; }
    public int ActivityCount { get; init; }
}

#endregion
