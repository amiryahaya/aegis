using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Service for real-time notification delivery via SignalR
/// </summary>
public interface INotificationHub
{
    /// <summary>
    /// Sends a notification to a specific user
    /// </summary>
    Task<Result> SendToUserAsync(
        Guid userId,
        Notification notification,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a notification to all members of a team
    /// </summary>
    Task<Result> SendToTeamAsync(
        Guid teamId,
        Notification notification,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a notification to all members of a workspace
    /// </summary>
    Task<Result> SendToWorkspaceAsync(
        Guid workspaceId,
        Notification notification,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcasts a notification to all connected clients
    /// </summary>
    Task<Result> BroadcastAsync(
        Notification notification,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an unread count update to a specific user
    /// </summary>
    Task<Result> SendUnreadCountAsync(
        Guid userId,
        int unreadCount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies a user that a notification was marked as read
    /// </summary>
    Task<Result> SendNotificationReadAsync(
        Guid userId,
        Guid notificationId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Real-time notification payload sent to clients
/// </summary>
public record NotificationPayload
{
    public required Guid Id { get; init; }
    public required string Type { get; init; }
    public required string Priority { get; init; }
    public required string Title { get; init; }
    public required string Message { get; init; }
    public string? ActionUrl { get; init; }
    public string? ActionLabel { get; init; }
    public Dictionary<string, object>? Data { get; init; }
    public Guid? RelatedEntityId { get; init; }
    public string? RelatedEntityType { get; init; }
    public DateTime CreatedAt { get; init; }

    public static NotificationPayload FromNotification(Notification notification) => new()
    {
        Id = notification.Id,
        Type = notification.Type.ToString(),
        Priority = notification.Priority.ToString(),
        Title = notification.Title,
        Message = notification.Message,
        ActionUrl = notification.ActionUrl,
        ActionLabel = notification.ActionLabel,
        Data = notification.Data.Count > 0 ? notification.Data : null,
        RelatedEntityId = notification.RelatedEntityId,
        RelatedEntityType = notification.RelatedEntityType,
        CreatedAt = notification.CreatedAt
    };
}
