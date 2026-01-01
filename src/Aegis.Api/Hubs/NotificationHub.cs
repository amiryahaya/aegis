using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Aegis.Api.Hubs;

/// <summary>
/// SignalR hub for real-time notifications
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(
        INotificationService notificationService,
        ILogger<NotificationHub> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId.HasValue)
        {
            // Add user to their personal group
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");

            // Send current unread count
            var unreadResult = await _notificationService.GetUnreadCountAsync(userId.Value);
            if (unreadResult.IsSuccess)
            {
                await Clients.Caller.SendAsync("UnreadCount", unreadResult.Value);
            }

            _logger.LogInformation("User {UserId} connected to notification hub", userId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (userId.HasValue)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
            _logger.LogInformation("User {UserId} disconnected from notification hub", userId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Subscribes to team notifications
    /// </summary>
    public async Task JoinTeam(Guid teamId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"team_{teamId}");
        _logger.LogDebug("Connection {ConnectionId} joined team {TeamId}", Context.ConnectionId, teamId);
    }

    /// <summary>
    /// Unsubscribes from team notifications
    /// </summary>
    public async Task LeaveTeam(Guid teamId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"team_{teamId}");
        _logger.LogDebug("Connection {ConnectionId} left team {TeamId}", Context.ConnectionId, teamId);
    }

    /// <summary>
    /// Subscribes to workspace notifications
    /// </summary>
    public async Task JoinWorkspace(Guid workspaceId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"workspace_{workspaceId}");
        _logger.LogDebug("Connection {ConnectionId} joined workspace {WorkspaceId}", Context.ConnectionId, workspaceId);
    }

    /// <summary>
    /// Unsubscribes from workspace notifications
    /// </summary>
    public async Task LeaveWorkspace(Guid workspaceId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"workspace_{workspaceId}");
        _logger.LogDebug("Connection {ConnectionId} left workspace {WorkspaceId}", Context.ConnectionId, workspaceId);
    }

    /// <summary>
    /// Marks a notification as read
    /// </summary>
    public async Task MarkAsRead(Guid notificationId)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            await Clients.Caller.SendAsync("Error", "Unauthorized");
            return;
        }

        var result = await _notificationService.MarkAsReadAsync(notificationId);
        if (result.IsSuccess)
        {
            var unreadResult = await _notificationService.GetUnreadCountAsync(userId.Value);
            if (unreadResult.IsSuccess)
            {
                await Clients.Caller.SendAsync("UnreadCount", unreadResult.Value);
            }
            await Clients.Caller.SendAsync("NotificationRead", notificationId);
        }
        else
        {
            await Clients.Caller.SendAsync("Error", result.Error!.Message);
        }
    }

    /// <summary>
    /// Marks all notifications as read
    /// </summary>
    public async Task MarkAllAsRead()
    {
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            await Clients.Caller.SendAsync("Error", "Unauthorized");
            return;
        }

        var result = await _notificationService.MarkAllAsReadAsync(userId.Value);
        if (result.IsSuccess)
        {
            await Clients.Caller.SendAsync("UnreadCount", 0);
            await Clients.Caller.SendAsync("AllNotificationsRead", result.Value);
        }
        else
        {
            await Clients.Caller.SendAsync("Error", result.Error!.Message);
        }
    }

    /// <summary>
    /// Gets recent notifications
    /// </summary>
    public async Task GetRecent(int count = 10)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            await Clients.Caller.SendAsync("Error", "Unauthorized");
            return;
        }

        var filter = new NotificationFilter { PageSize = count };
        var result = await _notificationService.GetForUserAsync(userId.Value, filter);

        if (result.IsSuccess)
        {
            var payloads = result.Value.Items
                .Select(NotificationPayload.FromNotification)
                .ToList();
            await Clients.Caller.SendAsync("RecentNotifications", payloads);
        }
        else
        {
            await Clients.Caller.SendAsync("Error", result.Error!.Message);
        }
    }

    private Guid? GetUserId()
    {
        var userIdClaim = Context.User?.FindFirst("sub")?.Value
            ?? Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (Guid.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }

        return null;
    }
}

/// <summary>
/// Service for sending notifications through the NotificationHub
/// </summary>
public class SignalRNotificationHub : INotificationHub
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<SignalRNotificationHub> _logger;

    public SignalRNotificationHub(
        IHubContext<NotificationHub> hubContext,
        ILogger<SignalRNotificationHub> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<Result> SendToUserAsync(
        Guid userId,
        Notification notification,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = NotificationPayload.FromNotification(notification);
            await _hubContext.Clients
                .Group($"user_{userId}")
                .SendAsync("Notification", payload, cancellationToken);

            _logger.LogDebug("Sent notification {NotificationId} to user {UserId}", notification.Id, userId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification to user {UserId}", userId);
            return Result.Failure(Error.Internal("NotificationHub.SendFailed", "Failed to send notification"));
        }
    }

    public async Task<Result> SendToTeamAsync(
        Guid teamId,
        Notification notification,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = NotificationPayload.FromNotification(notification);
            await _hubContext.Clients
                .Group($"team_{teamId}")
                .SendAsync("Notification", payload, cancellationToken);

            _logger.LogDebug("Sent notification {NotificationId} to team {TeamId}", notification.Id, teamId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification to team {TeamId}", teamId);
            return Result.Failure(Error.Internal("NotificationHub.SendFailed", "Failed to send notification"));
        }
    }

    public async Task<Result> SendToWorkspaceAsync(
        Guid workspaceId,
        Notification notification,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = NotificationPayload.FromNotification(notification);
            await _hubContext.Clients
                .Group($"workspace_{workspaceId}")
                .SendAsync("Notification", payload, cancellationToken);

            _logger.LogDebug("Sent notification {NotificationId} to workspace {WorkspaceId}", notification.Id, workspaceId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification to workspace {WorkspaceId}", workspaceId);
            return Result.Failure(Error.Internal("NotificationHub.SendFailed", "Failed to send notification"));
        }
    }

    public async Task<Result> BroadcastAsync(
        Notification notification,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = NotificationPayload.FromNotification(notification);
            await _hubContext.Clients.All.SendAsync("Notification", payload, cancellationToken);

            _logger.LogDebug("Broadcast notification {NotificationId}", notification.Id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast notification");
            return Result.Failure(Error.Internal("NotificationHub.BroadcastFailed", "Failed to broadcast notification"));
        }
    }

    public async Task<Result> SendUnreadCountAsync(
        Guid userId,
        int unreadCount,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _hubContext.Clients
                .Group($"user_{userId}")
                .SendAsync("UnreadCount", unreadCount, cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send unread count to user {UserId}", userId);
            return Result.Failure(Error.Internal("NotificationHub.SendFailed", "Failed to send unread count"));
        }
    }

    public async Task<Result> SendNotificationReadAsync(
        Guid userId,
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _hubContext.Clients
                .Group($"user_{userId}")
                .SendAsync("NotificationRead", notificationId, cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification read to user {UserId}", userId);
            return Result.Failure(Error.Internal("NotificationHub.SendFailed", "Failed to send notification read"));
        }
    }
}
