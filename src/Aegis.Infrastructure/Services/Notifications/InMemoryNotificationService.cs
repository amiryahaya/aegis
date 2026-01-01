using System.Collections.Concurrent;
using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Notifications;

public class InMemoryNotificationService : INotificationService
{
    private readonly ConcurrentDictionary<Guid, Notification> _notifications = new();
    private readonly ConcurrentDictionary<Guid, List<Guid>> _userNotifications = new();
    private readonly ILogger<InMemoryNotificationService> _logger;

    public InMemoryNotificationService(ILogger<InMemoryNotificationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<Result<Notification>> SendAsync(
        SendNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var notification = new Notification
            {
                UserId = request.UserId,
                Type = request.Type,
                Priority = request.Priority,
                Title = request.Title,
                Message = request.Message,
                ActionUrl = request.ActionUrl,
                ActionLabel = request.ActionLabel,
                Channel = request.Channels,
                Data = request.Data ?? new Dictionary<string, object>(),
                RelatedEntityId = request.RelatedEntityId,
                RelatedEntityType = request.RelatedEntityType,
                TeamId = request.TeamId,
                WorkspaceId = request.WorkspaceId,
                ExpiresAt = request.ExpiresAt
            };

            _notifications[notification.Id] = notification;

            // Track user notifications
            _userNotifications.AddOrUpdate(
                request.UserId,
                _ => new List<Guid> { notification.Id },
                (_, list) =>
                {
                    list.Add(notification.Id);
                    return list;
                });

            _logger.LogInformation(
                "Notification {NotificationId} of type {Type} sent to user {UserId}",
                notification.Id, notification.Type, notification.UserId);

            return Task.FromResult(Result<Notification>.Success(notification));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification to user {UserId}", request.UserId);
            return Task.FromResult(Result<Notification>.Failure(
                Error.Internal("Notification.SendFailed", "Failed to send notification")));
        }
    }

    public async Task<Result<int>> SendToTeamAsync(
        Guid teamId,
        SendNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        // In a real implementation, we would look up team members from a repository
        // For in-memory, we'll just send to the user specified in the request
        var result = await SendAsync(request with { TeamId = teamId }, cancellationToken);
        return result.IsSuccess
            ? Result<int>.Success(1)
            : Result<int>.Failure(result.Error!);
    }

    public async Task<Result<int>> SendToWorkspaceAsync(
        Guid workspaceId,
        SendNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        // In a real implementation, we would look up workspace members from a repository
        // For in-memory, we'll just send to the user specified in the request
        var result = await SendAsync(request with { WorkspaceId = workspaceId }, cancellationToken);
        return result.IsSuccess
            ? Result<int>.Success(1)
            : Result<int>.Failure(result.Error!);
    }

    public Task<Result<Notification>> GetByIdAsync(
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        if (_notifications.TryGetValue(notificationId, out var notification))
        {
            return Task.FromResult(Result<Notification>.Success(notification));
        }

        return Task.FromResult(Result<Notification>.Failure(
            Error.NotFound("Notification.NotFound", $"Notification {notificationId} not found")));
    }

    public Task<Result<NotificationPage>> GetForUserAsync(
        Guid userId,
        NotificationFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        filter ??= new NotificationFilter();

        if (!_userNotifications.TryGetValue(userId, out var notificationIds))
        {
            return Task.FromResult(Result<NotificationPage>.Success(new NotificationPage
            {
                Items = Array.Empty<Notification>(),
                TotalCount = 0,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            }));
        }

        var notifications = notificationIds
            .Select(id => _notifications.TryGetValue(id, out var n) ? n : null)
            .Where(n => n != null)
            .Cast<Notification>();

        // Apply filters
        if (filter.Type.HasValue)
        {
            notifications = notifications.Where(n => n.Type == filter.Type.Value);
        }

        if (filter.Status.HasValue)
        {
            notifications = notifications.Where(n => n.Status == filter.Status.Value);
        }

        if (filter.Priority.HasValue)
        {
            notifications = notifications.Where(n => n.Priority == filter.Priority.Value);
        }

        if (filter.TeamId.HasValue)
        {
            notifications = notifications.Where(n => n.TeamId == filter.TeamId.Value);
        }

        if (filter.WorkspaceId.HasValue)
        {
            notifications = notifications.Where(n => n.WorkspaceId == filter.WorkspaceId.Value);
        }

        if (filter.Since.HasValue)
        {
            notifications = notifications.Where(n => n.CreatedAt >= filter.Since.Value);
        }

        if (filter.Until.HasValue)
        {
            notifications = notifications.Where(n => n.CreatedAt <= filter.Until.Value);
        }

        if (!filter.IncludeArchived)
        {
            notifications = notifications.Where(n => n.Status != NotificationStatus.Archived);
        }

        if (!filter.IncludeExpired)
        {
            notifications = notifications.Where(n =>
                n.Status != NotificationStatus.Expired &&
                (!n.ExpiresAt.HasValue || n.ExpiresAt.Value > DateTime.UtcNow));
        }

        // Order by creation date descending (newest first)
        var orderedNotifications = notifications.OrderByDescending(n => n.CreatedAt).ToList();

        var totalCount = orderedNotifications.Count;
        var pagedItems = orderedNotifications
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToList();

        return Task.FromResult(Result<NotificationPage>.Success(new NotificationPage
        {
            Items = pagedItems,
            TotalCount = totalCount,
            PageNumber = filter.PageNumber,
            PageSize = filter.PageSize
        }));
    }

    public Task<Result<int>> GetUnreadCountAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (!_userNotifications.TryGetValue(userId, out var notificationIds))
        {
            return Task.FromResult(Result<int>.Success(0));
        }

        var count = notificationIds
            .Select(id => _notifications.TryGetValue(id, out var n) ? n : null)
            .Where(n => n != null)
            .Count(n => n!.Status == NotificationStatus.Unread &&
                       (!n.ExpiresAt.HasValue || n.ExpiresAt.Value > DateTime.UtcNow));

        return Task.FromResult(Result<int>.Success(count));
    }

    public Task<Result> MarkAsReadAsync(
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        if (!_notifications.TryGetValue(notificationId, out var notification))
        {
            return Task.FromResult(Result.Failure(
                Error.NotFound("Notification.NotFound", $"Notification {notificationId} not found")));
        }

        if (notification.Status == NotificationStatus.Read)
        {
            return Task.FromResult(Result.Success());
        }

        var updated = notification with
        {
            Status = NotificationStatus.Read,
            ReadAt = DateTime.UtcNow
        };

        _notifications[notificationId] = updated;

        _logger.LogDebug("Notification {NotificationId} marked as read", notificationId);

        return Task.FromResult(Result.Success());
    }

    public Task<Result<int>> MarkAllAsReadAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (!_userNotifications.TryGetValue(userId, out var notificationIds))
        {
            return Task.FromResult(Result<int>.Success(0));
        }

        var count = 0;
        var now = DateTime.UtcNow;

        foreach (var id in notificationIds)
        {
            if (_notifications.TryGetValue(id, out var notification) &&
                notification.Status == NotificationStatus.Unread)
            {
                _notifications[id] = notification with
                {
                    Status = NotificationStatus.Read,
                    ReadAt = now
                };
                count++;
            }
        }

        _logger.LogInformation("Marked {Count} notifications as read for user {UserId}", count, userId);

        return Task.FromResult(Result<int>.Success(count));
    }

    public Task<Result> ArchiveAsync(
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        if (!_notifications.TryGetValue(notificationId, out var notification))
        {
            return Task.FromResult(Result.Failure(
                Error.NotFound("Notification.NotFound", $"Notification {notificationId} not found")));
        }

        var updated = notification with
        {
            Status = NotificationStatus.Archived,
            ArchivedAt = DateTime.UtcNow
        };

        _notifications[notificationId] = updated;

        _logger.LogDebug("Notification {NotificationId} archived", notificationId);

        return Task.FromResult(Result.Success());
    }

    public Task<Result> DeleteAsync(
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        if (!_notifications.TryRemove(notificationId, out var notification))
        {
            return Task.FromResult(Result.Failure(
                Error.NotFound("Notification.NotFound", $"Notification {notificationId} not found")));
        }

        // Remove from user notifications
        if (_userNotifications.TryGetValue(notification.UserId, out var list))
        {
            list.Remove(notificationId);
        }

        _logger.LogDebug("Notification {NotificationId} deleted", notificationId);

        return Task.FromResult(Result.Success());
    }

    public Task<Result<int>> DeleteArchivedBeforeAsync(
        DateTime before,
        CancellationToken cancellationToken = default)
    {
        var toDelete = _notifications.Values
            .Where(n => n.Status == NotificationStatus.Archived && n.ArchivedAt < before)
            .ToList();

        var count = 0;
        foreach (var notification in toDelete)
        {
            if (_notifications.TryRemove(notification.Id, out _))
            {
                if (_userNotifications.TryGetValue(notification.UserId, out var list))
                {
                    list.Remove(notification.Id);
                }
                count++;
            }
        }

        _logger.LogInformation("Deleted {Count} archived notifications before {Before}", count, before);

        return Task.FromResult(Result<int>.Success(count));
    }

    public Task<Result<Notification>> CreateFromEventAsync(
        DomainEvent domainEvent,
        Guid recipientUserId,
        CancellationToken cancellationToken = default)
    {
        var request = domainEvent switch
        {
            DocumentProcessedEvent e => NotificationTemplates.DocumentProcessed(
                recipientUserId, e.DocumentId, e.FileName, e.ChunkCount, e.TeamId),

            DocumentProcessingFailedEvent e => NotificationTemplates.DocumentProcessingFailed(
                recipientUserId, e.DocumentId, e.FileName, e.ErrorMessage, e.TeamId),

            QueryCompletedEvent e => NotificationTemplates.QueryCompleted(
                recipientUserId, e.QueryId, e.Query, e.SourceCount, e.ProcessingTime),

            DataSourceSyncCompletedEvent e => NotificationTemplates.SyncCompleted(
                recipientUserId, e.DataSourceId, e.DataSourceName, e.DocumentsProcessed, e.TeamId),

            DataSourceSyncFailedEvent e => NotificationTemplates.SyncFailed(
                recipientUserId, e.DataSourceId, e.DataSourceName, e.ErrorMessage, e.TeamId),

            RateLimitExceededEvent e => NotificationTemplates.RateLimitWarning(
                recipientUserId, e.Endpoint, e.CurrentCount, e.Limit),

            ApiKeyRevokedEvent e => new SendNotificationRequest
            {
                UserId = recipientUserId,
                Type = NotificationType.ApiKeyRevoked,
                Title = "API Key Revoked",
                Message = $"API key '{e.KeyName}' has been revoked: {e.Reason}",
                Priority = NotificationPriority.High,
                ActionUrl = "/settings/api-keys",
                ActionLabel = "Manage API Keys"
            },

            UserCreatedEvent e => NotificationTemplates.Welcome(recipientUserId, e.DisplayName),

            _ => new SendNotificationRequest
            {
                UserId = recipientUserId,
                Type = NotificationType.Custom,
                Title = "Event Notification",
                Message = $"Event {domainEvent.WebhookEventType} occurred",
                Priority = NotificationPriority.Normal,
                Data = new Dictionary<string, object>
                {
                    ["eventType"] = domainEvent.WebhookEventType.ToString(),
                    ["eventId"] = domainEvent.Id
                }
            }
        };

        return SendAsync(request, cancellationToken);
    }

    public Task<Result<NotificationStats>> GetStatsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (!_userNotifications.TryGetValue(userId, out var notificationIds))
        {
            return Task.FromResult(Result<NotificationStats>.Success(new NotificationStats
            {
                UserId = userId,
                TotalCount = 0,
                UnreadCount = 0,
                ReadCount = 0,
                ArchivedCount = 0
            }));
        }

        var notifications = notificationIds
            .Select(id => _notifications.TryGetValue(id, out var n) ? n : null)
            .Where(n => n != null)
            .Cast<Notification>()
            .ToList();

        var stats = new NotificationStats
        {
            UserId = userId,
            TotalCount = notifications.Count,
            UnreadCount = notifications.Count(n => n.Status == NotificationStatus.Unread),
            ReadCount = notifications.Count(n => n.Status == NotificationStatus.Read),
            ArchivedCount = notifications.Count(n => n.Status == NotificationStatus.Archived),
            CountByType = notifications
                .GroupBy(n => n.Type)
                .ToDictionary(g => g.Key, g => g.Count()),
            CountByPriority = notifications
                .GroupBy(n => n.Priority)
                .ToDictionary(g => g.Key, g => g.Count()),
            LastNotificationAt = notifications.OrderByDescending(n => n.CreatedAt).FirstOrDefault()?.CreatedAt,
            LastReadAt = notifications
                .Where(n => n.ReadAt.HasValue)
                .OrderByDescending(n => n.ReadAt)
                .FirstOrDefault()?.ReadAt
        };

        return Task.FromResult(Result<NotificationStats>.Success(stats));
    }
}
