using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Service for managing user notifications
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Creates and sends a notification to a user
    /// </summary>
    Task<Result<Notification>> SendAsync(
        SendNotificationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a notification to all members of a team
    /// </summary>
    Task<Result<int>> SendToTeamAsync(
        Guid teamId,
        SendNotificationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a notification to all members of a workspace
    /// </summary>
    Task<Result<int>> SendToWorkspaceAsync(
        Guid workspaceId,
        SendNotificationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a notification by ID
    /// </summary>
    Task<Result<Notification>> GetByIdAsync(
        Guid notificationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets notifications for a user with optional filtering
    /// </summary>
    Task<Result<NotificationPage>> GetForUserAsync(
        Guid userId,
        NotificationFilter? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets unread notification count for a user
    /// </summary>
    Task<Result<int>> GetUnreadCountAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a notification as read
    /// </summary>
    Task<Result> MarkAsReadAsync(
        Guid notificationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks all notifications as read for a user
    /// </summary>
    Task<Result<int>> MarkAllAsReadAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Archives a notification
    /// </summary>
    Task<Result> ArchiveAsync(
        Guid notificationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a notification
    /// </summary>
    Task<Result> DeleteAsync(
        Guid notificationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all archived notifications older than the specified date
    /// </summary>
    Task<Result<int>> DeleteArchivedBeforeAsync(
        DateTime before,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a notification from a domain event
    /// </summary>
    Task<Result<Notification>> CreateFromEventAsync(
        DomainEvent domainEvent,
        Guid recipientUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets notification statistics for a user
    /// </summary>
    Task<Result<NotificationStats>> GetStatsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A user notification
/// </summary>
public record Notification
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid UserId { get; init; }
    public required NotificationType Type { get; init; }
    public required NotificationPriority Priority { get; init; }
    public required string Title { get; init; }
    public required string Message { get; init; }
    public string? ActionUrl { get; init; }
    public string? ActionLabel { get; init; }
    public NotificationStatus Status { get; init; } = NotificationStatus.Unread;
    public NotificationChannel Channel { get; init; } = NotificationChannel.InApp;
    public Dictionary<string, object> Data { get; init; } = new();
    public Guid? RelatedEntityId { get; init; }
    public string? RelatedEntityType { get; init; }
    public Guid? TeamId { get; init; }
    public Guid? WorkspaceId { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; init; }
    public DateTime? ArchivedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
}

/// <summary>
/// Types of notifications
/// </summary>
public enum NotificationType
{
    // Query notifications
    QueryCompleted,
    QueryFailed,

    // Document notifications
    DocumentUploaded,
    DocumentProcessed,
    DocumentProcessingFailed,
    DocumentDeleted,

    // Data source notifications
    SyncStarted,
    SyncCompleted,
    SyncFailed,

    // System notifications
    SystemAlert,
    MaintenanceScheduled,
    ServiceDegraded,

    // User notifications
    WelcomeMessage,
    TeamInvitation,
    TeamRemoval,
    RoleChanged,

    // Security notifications
    RateLimitWarning,
    ApiKeyExpiring,
    ApiKeyRevoked,
    SuspiciousActivity,

    // Webhook notifications
    WebhookDeliveryFailed,
    WebhookDisabled,

    // Feature notifications
    FeatureFlagChanged,
    ConfigurationChanged,

    // Feedback notifications
    FeedbackReceived,
    FeedbackResolved,

    // Custom
    Custom
}

/// <summary>
/// Notification priority levels
/// </summary>
public enum NotificationPriority
{
    Low,
    Normal,
    High,
    Urgent
}

/// <summary>
/// Notification status
/// </summary>
public enum NotificationStatus
{
    Unread,
    Read,
    Archived,
    Expired
}

/// <summary>
/// Notification delivery channels
/// </summary>
[Flags]
public enum NotificationChannel
{
    None = 0,
    InApp = 1,
    Email = 2,
    RealTime = 4,
    Webhook = 8,
    All = InApp | Email | RealTime | Webhook
}

/// <summary>
/// Request to send a notification
/// </summary>
public record SendNotificationRequest
{
    public required Guid UserId { get; init; }
    public required NotificationType Type { get; init; }
    public required string Title { get; init; }
    public required string Message { get; init; }
    public NotificationPriority Priority { get; init; } = NotificationPriority.Normal;
    public NotificationChannel Channels { get; init; } = NotificationChannel.InApp | NotificationChannel.RealTime;
    public string? ActionUrl { get; init; }
    public string? ActionLabel { get; init; }
    public Dictionary<string, object>? Data { get; init; }
    public Guid? RelatedEntityId { get; init; }
    public string? RelatedEntityType { get; init; }
    public Guid? TeamId { get; init; }
    public Guid? WorkspaceId { get; init; }
    public DateTime? ExpiresAt { get; init; }
}

/// <summary>
/// Filter for querying notifications
/// </summary>
public record NotificationFilter
{
    public NotificationType? Type { get; init; }
    public NotificationStatus? Status { get; init; }
    public NotificationPriority? Priority { get; init; }
    public Guid? TeamId { get; init; }
    public Guid? WorkspaceId { get; init; }
    public DateTime? Since { get; init; }
    public DateTime? Until { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public bool IncludeArchived { get; init; } = false;
    public bool IncludeExpired { get; init; } = false;
}

/// <summary>
/// Paginated notification results
/// </summary>
public record NotificationPage
{
    public required IReadOnlyList<Notification> Items { get; init; }
    public int TotalCount { get; init; }
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => PageNumber < TotalPages;
    public bool HasPreviousPage => PageNumber > 1;
}

/// <summary>
/// Notification statistics for a user
/// </summary>
public record NotificationStats
{
    public Guid UserId { get; init; }
    public int TotalCount { get; init; }
    public int UnreadCount { get; init; }
    public int ReadCount { get; init; }
    public int ArchivedCount { get; init; }
    public Dictionary<NotificationType, int> CountByType { get; init; } = new();
    public Dictionary<NotificationPriority, int> CountByPriority { get; init; } = new();
    public DateTime? LastNotificationAt { get; init; }
    public DateTime? LastReadAt { get; init; }
}

/// <summary>
/// Well-known notification templates
/// </summary>
public static class NotificationTemplates
{
    public static SendNotificationRequest QueryCompleted(Guid userId, Guid queryId, string query, int sourceCount, TimeSpan processingTime)
        => new()
        {
            UserId = userId,
            Type = NotificationType.QueryCompleted,
            Title = "Query Completed",
            Message = $"Your query has been processed with {sourceCount} sources in {processingTime.TotalSeconds:F1}s",
            Priority = NotificationPriority.Normal,
            ActionUrl = $"/queries/{queryId}",
            ActionLabel = "View Results",
            RelatedEntityId = queryId,
            RelatedEntityType = "Query",
            Data = new Dictionary<string, object>
            {
                ["query"] = query,
                ["sourceCount"] = sourceCount,
                ["processingTimeMs"] = processingTime.TotalMilliseconds
            }
        };

    public static SendNotificationRequest DocumentProcessed(Guid userId, Guid documentId, string fileName, int chunkCount, Guid? teamId = null)
        => new()
        {
            UserId = userId,
            Type = NotificationType.DocumentProcessed,
            Title = "Document Processed",
            Message = $"'{fileName}' has been processed and indexed ({chunkCount} chunks)",
            Priority = NotificationPriority.Normal,
            ActionUrl = $"/documents/{documentId}",
            ActionLabel = "View Document",
            RelatedEntityId = documentId,
            RelatedEntityType = "Document",
            TeamId = teamId,
            Data = new Dictionary<string, object>
            {
                ["fileName"] = fileName,
                ["chunkCount"] = chunkCount
            }
        };

    public static SendNotificationRequest DocumentProcessingFailed(Guid userId, Guid documentId, string fileName, string error, Guid? teamId = null)
        => new()
        {
            UserId = userId,
            Type = NotificationType.DocumentProcessingFailed,
            Title = "Document Processing Failed",
            Message = $"Failed to process '{fileName}': {error}",
            Priority = NotificationPriority.High,
            ActionUrl = $"/documents/{documentId}",
            ActionLabel = "View Details",
            RelatedEntityId = documentId,
            RelatedEntityType = "Document",
            TeamId = teamId,
            Data = new Dictionary<string, object>
            {
                ["fileName"] = fileName,
                ["error"] = error
            }
        };

    public static SendNotificationRequest SyncCompleted(Guid userId, Guid dataSourceId, string dataSourceName, int documentsProcessed, Guid? teamId = null)
        => new()
        {
            UserId = userId,
            Type = NotificationType.SyncCompleted,
            Title = "Sync Completed",
            Message = $"'{dataSourceName}' sync completed: {documentsProcessed} documents processed",
            Priority = NotificationPriority.Normal,
            ActionUrl = $"/data-sources/{dataSourceId}",
            ActionLabel = "View Data Source",
            RelatedEntityId = dataSourceId,
            RelatedEntityType = "DataSource",
            TeamId = teamId,
            Data = new Dictionary<string, object>
            {
                ["dataSourceName"] = dataSourceName,
                ["documentsProcessed"] = documentsProcessed
            }
        };

    public static SendNotificationRequest SyncFailed(Guid userId, Guid dataSourceId, string dataSourceName, string error, Guid? teamId = null)
        => new()
        {
            UserId = userId,
            Type = NotificationType.SyncFailed,
            Title = "Sync Failed",
            Message = $"'{dataSourceName}' sync failed: {error}",
            Priority = NotificationPriority.High,
            ActionUrl = $"/data-sources/{dataSourceId}",
            ActionLabel = "View Details",
            RelatedEntityId = dataSourceId,
            RelatedEntityType = "DataSource",
            TeamId = teamId,
            Data = new Dictionary<string, object>
            {
                ["dataSourceName"] = dataSourceName,
                ["error"] = error
            }
        };

    public static SendNotificationRequest RateLimitWarning(Guid userId, string endpoint, int currentCount, int limit)
        => new()
        {
            UserId = userId,
            Type = NotificationType.RateLimitWarning,
            Title = "Rate Limit Warning",
            Message = $"You are approaching the rate limit for {endpoint} ({currentCount}/{limit})",
            Priority = NotificationPriority.High,
            Data = new Dictionary<string, object>
            {
                ["endpoint"] = endpoint,
                ["currentCount"] = currentCount,
                ["limit"] = limit
            }
        };

    public static SendNotificationRequest ApiKeyExpiring(Guid userId, string keyName, DateTime expiresAt)
        => new()
        {
            UserId = userId,
            Type = NotificationType.ApiKeyExpiring,
            Title = "API Key Expiring",
            Message = $"Your API key '{keyName}' will expire on {expiresAt:d}",
            Priority = NotificationPriority.High,
            ActionUrl = "/settings/api-keys",
            ActionLabel = "Manage API Keys",
            Data = new Dictionary<string, object>
            {
                ["keyName"] = keyName,
                ["expiresAt"] = expiresAt
            }
        };

    public static SendNotificationRequest WebhookDeliveryFailed(Guid userId, Guid webhookId, string webhookName, int failureCount)
        => new()
        {
            UserId = userId,
            Type = NotificationType.WebhookDeliveryFailed,
            Title = "Webhook Delivery Failed",
            Message = $"Webhook '{webhookName}' has failed {failureCount} consecutive deliveries",
            Priority = NotificationPriority.High,
            ActionUrl = $"/webhooks/{webhookId}",
            ActionLabel = "View Webhook",
            RelatedEntityId = webhookId,
            RelatedEntityType = "Webhook",
            Data = new Dictionary<string, object>
            {
                ["webhookName"] = webhookName,
                ["failureCount"] = failureCount
            }
        };

    public static SendNotificationRequest SystemAlert(Guid userId, string title, string message, NotificationPriority priority = NotificationPriority.High)
        => new()
        {
            UserId = userId,
            Type = NotificationType.SystemAlert,
            Title = title,
            Message = message,
            Priority = priority
        };

    public static SendNotificationRequest Welcome(Guid userId, string displayName)
        => new()
        {
            UserId = userId,
            Type = NotificationType.WelcomeMessage,
            Title = "Welcome to AEGIS!",
            Message = $"Hello {displayName}! Welcome to AEGIS. Get started by uploading your first document or connecting a data source.",
            Priority = NotificationPriority.Normal,
            ActionUrl = "/getting-started",
            ActionLabel = "Get Started"
        };
}
