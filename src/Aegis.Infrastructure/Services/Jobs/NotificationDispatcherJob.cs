using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Jobs;

/// <summary>
/// Background job for processing and dispatching notifications
/// </summary>
public class NotificationDispatcherJob
{
    private readonly INotificationService _notificationService;
    private readonly INotificationHub _notificationHub;
    private readonly IUserPreferencesService _preferencesService;
    private readonly ILogger<NotificationDispatcherJob> _logger;

    public NotificationDispatcherJob(
        INotificationService notificationService,
        INotificationHub notificationHub,
        IUserPreferencesService preferencesService,
        ILogger<NotificationDispatcherJob> logger)
    {
        _notificationService = notificationService;
        _notificationHub = notificationHub;
        _preferencesService = preferencesService;
        _logger = logger;
    }

    /// <summary>
    /// Sends a notification with real-time delivery based on user preferences
    /// </summary>
    public async Task SendNotificationAsync(SendNotificationRequest request)
    {
        try
        {
            _logger.LogInformation(
                "Processing notification of type {Type} for user {UserId}",
                request.Type, request.UserId);

            // Check user notification preferences
            var prefsResult = await _preferencesService.GetAllAsync(request.UserId);
            if (prefsResult.IsSuccess)
            {
                var prefs = prefsResult.Value;

                // Check if this notification type is allowed based on preferences
                if (!ShouldSendNotification(request.Type, prefs))
                {
                    _logger.LogDebug(
                        "Notification of type {Type} suppressed for user {UserId} based on preferences",
                        request.Type, request.UserId);
                    return;
                }
            }

            // Create the notification
            var result = await _notificationService.SendAsync(request);
            if (result.IsFailure)
            {
                _logger.LogError(
                    "Failed to create notification: {Error}",
                    result.Error!.Message);
                return;
            }

            var notification = result.Value;

            // Send real-time notification if enabled
            if (request.Channels.HasFlag(NotificationChannel.RealTime))
            {
                await _notificationHub.SendToUserAsync(request.UserId, notification);

                // Also update unread count
                var unreadResult = await _notificationService.GetUnreadCountAsync(request.UserId);
                if (unreadResult.IsSuccess)
                {
                    await _notificationHub.SendUnreadCountAsync(request.UserId, unreadResult.Value);
                }
            }

            // Send to team if applicable
            if (request.TeamId.HasValue && request.Channels.HasFlag(NotificationChannel.RealTime))
            {
                await _notificationHub.SendToTeamAsync(request.TeamId.Value, notification);
            }

            // Send to workspace if applicable
            if (request.WorkspaceId.HasValue && request.Channels.HasFlag(NotificationChannel.RealTime))
            {
                await _notificationHub.SendToWorkspaceAsync(request.WorkspaceId.Value, notification);
            }

            _logger.LogInformation(
                "Notification {NotificationId} delivered to user {UserId}",
                notification.Id, request.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing notification of type {Type} for user {UserId}",
                request.Type, request.UserId);
            throw;
        }
    }

    /// <summary>
    /// Sends a notification from a domain event
    /// </summary>
    public async Task SendFromEventAsync(DomainEvent domainEvent, Guid recipientUserId)
    {
        try
        {
            _logger.LogInformation(
                "Creating notification from event {EventType} for user {UserId}",
                domainEvent.WebhookEventType, recipientUserId);

            // Create notification from event
            var result = await _notificationService.CreateFromEventAsync(domainEvent, recipientUserId);
            if (result.IsFailure)
            {
                _logger.LogError(
                    "Failed to create notification from event: {Error}",
                    result.Error!.Message);
                return;
            }

            var notification = result.Value;

            // Send real-time notification
            await _notificationHub.SendToUserAsync(recipientUserId, notification);

            // Update unread count
            var unreadResult = await _notificationService.GetUnreadCountAsync(recipientUserId);
            if (unreadResult.IsSuccess)
            {
                await _notificationHub.SendUnreadCountAsync(recipientUserId, unreadResult.Value);
            }

            _logger.LogInformation(
                "Event notification {NotificationId} delivered to user {UserId}",
                notification.Id, recipientUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error creating notification from event {EventType} for user {UserId}",
                domainEvent.WebhookEventType, recipientUserId);
            throw;
        }
    }

    /// <summary>
    /// Cleans up old archived notifications
    /// </summary>
    public async Task CleanupArchivedNotificationsAsync(int daysToKeep = 30)
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-daysToKeep);
            var result = await _notificationService.DeleteArchivedBeforeAsync(cutoff);

            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "Cleaned up {Count} archived notifications older than {Days} days",
                    result.Value, daysToKeep);
            }
            else
            {
                _logger.LogError(
                    "Failed to cleanup archived notifications: {Error}",
                    result.Error!.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up archived notifications");
            throw;
        }
    }

    /// <summary>
    /// Broadcasts a system notification to all users
    /// </summary>
    public async Task BroadcastSystemNotificationAsync(
        string title,
        string message,
        NotificationPriority priority = NotificationPriority.Normal)
    {
        try
        {
            _logger.LogInformation("Broadcasting system notification: {Title}", title);

            // Create a broadcast notification (no specific user)
            var notification = new Notification
            {
                UserId = Guid.Empty, // System-level
                Type = NotificationType.SystemAlert,
                Priority = priority,
                Title = title,
                Message = message,
                Channel = NotificationChannel.RealTime
            };

            await _notificationHub.BroadcastAsync(notification);

            _logger.LogInformation("System notification broadcast completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting system notification");
            throw;
        }
    }

    private static bool ShouldSendNotification(NotificationType type, UserPreferences prefs)
    {
        // Check if in-app notifications are enabled
        if (!prefs.Notifications.InAppEnabled)
        {
            return false;
        }

        // Check if this notification type is muted
        var typeCategory = GetNotificationCategory(type);
        if (prefs.Notifications.MutedCategories.Contains(typeCategory))
        {
            return false;
        }

        // Check quiet hours (if enabled)
        if (prefs.Notifications.QuietHoursEnabled &&
            prefs.Notifications.QuietHoursStart.HasValue &&
            prefs.Notifications.QuietHoursEnd.HasValue)
        {
            var now = DateTime.UtcNow.TimeOfDay;
            var start = prefs.Notifications.QuietHoursStart.Value;
            var end = prefs.Notifications.QuietHoursEnd.Value;

            if (start < end)
            {
                // Simple case: start is before end (e.g., 22:00 - 06:00)
                if (now >= start && now < end)
                {
                    return false;
                }
            }
            else
            {
                // Overnight case: start is after end (e.g., 22:00 - 06:00)
                if (now >= start || now < end)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static string GetNotificationCategory(NotificationType type)
    {
        return type switch
        {
            NotificationType.QueryCompleted or
            NotificationType.QueryFailed => "queries",

            NotificationType.DocumentUploaded or
            NotificationType.DocumentProcessed or
            NotificationType.DocumentProcessingFailed or
            NotificationType.DocumentDeleted => "documents",

            NotificationType.SyncStarted or
            NotificationType.SyncCompleted or
            NotificationType.SyncFailed => "sync",

            NotificationType.SystemAlert or
            NotificationType.MaintenanceScheduled or
            NotificationType.ServiceDegraded => "system",

            NotificationType.RateLimitWarning or
            NotificationType.ApiKeyExpiring or
            NotificationType.ApiKeyRevoked or
            NotificationType.SuspiciousActivity => "security",

            NotificationType.WebhookDeliveryFailed or
            NotificationType.WebhookDisabled => "webhooks",

            NotificationType.TeamInvitation or
            NotificationType.TeamRemoval or
            NotificationType.RoleChanged => "team",

            _ => "other"
        };
    }
}
