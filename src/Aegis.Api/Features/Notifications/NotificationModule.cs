using Aegis.Domain.Services;
using Carter;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Aegis.Api.Features.Notifications;

public class NotificationModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notifications")
            .WithTags("Notifications");

        group.MapGet("/", GetNotifications)
            .WithSummary("Get notifications for the current user");

        group.MapGet("/{id:guid}", GetNotificationById)
            .WithSummary("Get a notification by ID");

        group.MapGet("/unread-count", GetUnreadCount)
            .WithSummary("Get unread notification count");

        group.MapGet("/stats", GetStats)
            .WithSummary("Get notification statistics");

        group.MapPut("/{id:guid}/read", MarkAsRead)
            .WithSummary("Mark a notification as read");

        group.MapPut("/read-all", MarkAllAsRead)
            .WithSummary("Mark all notifications as read");

        group.MapPut("/{id:guid}/archive", ArchiveNotification)
            .WithSummary("Archive a notification");

        group.MapDelete("/{id:guid}", DeleteNotification)
            .WithSummary("Delete a notification");

        group.MapPost("/send", SendNotification)
            .WithSummary("Send a notification (admin)");

        group.MapPost("/send-to-team/{teamId:guid}", SendToTeam)
            .WithSummary("Send a notification to all team members (admin)");

        group.MapGet("/types", GetNotificationTypes)
            .WithSummary("Get available notification types");
    }

    private static async Task<Ok<NotificationPageResponse>> GetNotifications(
        [FromQuery] Guid userId,
        [FromQuery] NotificationType? type,
        [FromQuery] NotificationStatus? status,
        [FromQuery] NotificationPriority? priority,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] bool? includeArchived,
        INotificationService notificationService)
    {
        var filter = new NotificationFilter
        {
            Type = type,
            Status = status,
            Priority = priority,
            PageNumber = page ?? 1,
            PageSize = pageSize ?? 20,
            IncludeArchived = includeArchived ?? false
        };

        var result = await notificationService.GetForUserAsync(userId, filter);

        var response = new NotificationPageResponse(
            result.Value.Items.Select(MapToResponse).ToList(),
            result.Value.TotalCount,
            result.Value.PageNumber,
            result.Value.PageSize,
            result.Value.TotalPages,
            result.Value.HasNextPage,
            result.Value.HasPreviousPage);

        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<NotificationResponse>, NotFound>> GetNotificationById(
        Guid id,
        INotificationService notificationService)
    {
        var result = await notificationService.GetByIdAsync(id);

        if (result.IsFailure)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(MapToResponse(result.Value));
    }

    private static async Task<Ok<int>> GetUnreadCount(
        [FromQuery] Guid userId,
        INotificationService notificationService)
    {
        var result = await notificationService.GetUnreadCountAsync(userId);
        return TypedResults.Ok(result.Value);
    }

    private static async Task<Ok<NotificationStatsResponse>> GetStats(
        [FromQuery] Guid userId,
        INotificationService notificationService)
    {
        var result = await notificationService.GetStatsAsync(userId);

        var response = new NotificationStatsResponse(
            result.Value.UserId,
            result.Value.TotalCount,
            result.Value.UnreadCount,
            result.Value.ReadCount,
            result.Value.ArchivedCount,
            result.Value.CountByType.ToDictionary(k => k.Key.ToString(), v => v.Value),
            result.Value.CountByPriority.ToDictionary(k => k.Key.ToString(), v => v.Value),
            result.Value.LastNotificationAt,
            result.Value.LastReadAt);

        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok, NotFound>> MarkAsRead(
        Guid id,
        INotificationService notificationService)
    {
        var result = await notificationService.MarkAsReadAsync(id);

        if (result.IsFailure)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok();
    }

    private static async Task<Ok<MarkAllReadResponse>> MarkAllAsRead(
        [FromQuery] Guid userId,
        INotificationService notificationService)
    {
        var result = await notificationService.MarkAllAsReadAsync(userId);
        return TypedResults.Ok(new MarkAllReadResponse(result.Value));
    }

    private static async Task<Results<Ok, NotFound>> ArchiveNotification(
        Guid id,
        INotificationService notificationService)
    {
        var result = await notificationService.ArchiveAsync(id);

        if (result.IsFailure)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok();
    }

    private static async Task<Results<NoContent, NotFound>> DeleteNotification(
        Guid id,
        INotificationService notificationService)
    {
        var result = await notificationService.DeleteAsync(id);

        if (result.IsFailure)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.NoContent();
    }

    private static async Task<Results<Created<NotificationResponse>, BadRequest<ProblemDetails>>> SendNotification(
        SendNotificationApiRequest request,
        INotificationService notificationService)
    {
        var sendRequest = new SendNotificationRequest
        {
            UserId = request.UserId,
            Type = request.Type,
            Title = request.Title,
            Message = request.Message,
            Priority = request.Priority ?? NotificationPriority.Normal,
            Channels = request.Channels ?? (NotificationChannel.InApp | NotificationChannel.RealTime),
            ActionUrl = request.ActionUrl,
            ActionLabel = request.ActionLabel,
            Data = request.Data,
            RelatedEntityId = request.RelatedEntityId,
            RelatedEntityType = request.RelatedEntityType,
            TeamId = request.TeamId,
            WorkspaceId = request.WorkspaceId,
            ExpiresAt = request.ExpiresAt
        };

        var result = await notificationService.SendAsync(sendRequest);

        if (result.IsFailure)
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = result.Error!.Code,
                Detail = result.Error.Message
            });
        }

        var response = MapToResponse(result.Value);
        return TypedResults.Created($"/api/notifications/{response.Id}", response);
    }

    private static async Task<Results<Ok<SendToGroupResponse>, BadRequest<ProblemDetails>>> SendToTeam(
        Guid teamId,
        SendNotificationApiRequest request,
        INotificationService notificationService)
    {
        var sendRequest = new SendNotificationRequest
        {
            UserId = request.UserId,
            Type = request.Type,
            Title = request.Title,
            Message = request.Message,
            Priority = request.Priority ?? NotificationPriority.Normal,
            Channels = request.Channels ?? (NotificationChannel.InApp | NotificationChannel.RealTime),
            ActionUrl = request.ActionUrl,
            ActionLabel = request.ActionLabel,
            Data = request.Data,
            RelatedEntityId = request.RelatedEntityId,
            RelatedEntityType = request.RelatedEntityType,
            TeamId = teamId,
            WorkspaceId = request.WorkspaceId,
            ExpiresAt = request.ExpiresAt
        };

        var result = await notificationService.SendToTeamAsync(teamId, sendRequest);

        if (result.IsFailure)
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = result.Error!.Code,
                Detail = result.Error.Message
            });
        }

        return TypedResults.Ok(new SendToGroupResponse(result.Value));
    }

    private static Ok<List<NotificationTypeInfo>> GetNotificationTypes()
    {
        var types = Enum.GetValues<NotificationType>()
            .Select(t => new NotificationTypeInfo(
                t.ToString(),
                GetTypeDescription(t),
                GetTypeCategory(t)))
            .ToList();

        return TypedResults.Ok(types);
    }

    private static NotificationResponse MapToResponse(Notification notification)
    {
        return new NotificationResponse(
            notification.Id,
            notification.UserId,
            notification.Type.ToString(),
            notification.Priority.ToString(),
            notification.Title,
            notification.Message,
            notification.ActionUrl,
            notification.ActionLabel,
            notification.Status.ToString(),
            notification.Data.Count > 0 ? notification.Data : null,
            notification.RelatedEntityId,
            notification.RelatedEntityType,
            notification.TeamId,
            notification.WorkspaceId,
            notification.CreatedAt,
            notification.ReadAt,
            notification.ArchivedAt,
            notification.ExpiresAt);
    }

    private static string GetTypeDescription(NotificationType type) => type switch
    {
        NotificationType.QueryCompleted => "Query has completed successfully",
        NotificationType.QueryFailed => "Query has failed",
        NotificationType.DocumentUploaded => "Document has been uploaded",
        NotificationType.DocumentProcessed => "Document has been processed and indexed",
        NotificationType.DocumentProcessingFailed => "Document processing has failed",
        NotificationType.DocumentDeleted => "Document has been deleted",
        NotificationType.SyncStarted => "Data source sync has started",
        NotificationType.SyncCompleted => "Data source sync has completed",
        NotificationType.SyncFailed => "Data source sync has failed",
        NotificationType.SystemAlert => "System alert",
        NotificationType.MaintenanceScheduled => "Scheduled maintenance notification",
        NotificationType.ServiceDegraded => "Service degradation notice",
        NotificationType.WelcomeMessage => "Welcome message for new users",
        NotificationType.TeamInvitation => "Team invitation",
        NotificationType.TeamRemoval => "Removed from team",
        NotificationType.RoleChanged => "Role has been changed",
        NotificationType.RateLimitWarning => "Approaching rate limit",
        NotificationType.ApiKeyExpiring => "API key is expiring soon",
        NotificationType.ApiKeyRevoked => "API key has been revoked",
        NotificationType.SuspiciousActivity => "Suspicious activity detected",
        NotificationType.WebhookDeliveryFailed => "Webhook delivery has failed",
        NotificationType.WebhookDisabled => "Webhook has been disabled",
        NotificationType.FeatureFlagChanged => "Feature flag has changed",
        NotificationType.ConfigurationChanged => "Configuration has changed",
        NotificationType.FeedbackReceived => "Feedback has been received",
        NotificationType.FeedbackResolved => "Feedback has been resolved",
        NotificationType.Custom => "Custom notification",
        _ => "Unknown notification type"
    };

    private static string GetTypeCategory(NotificationType type) => type switch
    {
        NotificationType.QueryCompleted or
        NotificationType.QueryFailed => "Query",

        NotificationType.DocumentUploaded or
        NotificationType.DocumentProcessed or
        NotificationType.DocumentProcessingFailed or
        NotificationType.DocumentDeleted => "Document",

        NotificationType.SyncStarted or
        NotificationType.SyncCompleted or
        NotificationType.SyncFailed => "DataSource",

        NotificationType.SystemAlert or
        NotificationType.MaintenanceScheduled or
        NotificationType.ServiceDegraded => "System",

        NotificationType.WelcomeMessage or
        NotificationType.TeamInvitation or
        NotificationType.TeamRemoval or
        NotificationType.RoleChanged => "User",

        NotificationType.RateLimitWarning or
        NotificationType.ApiKeyExpiring or
        NotificationType.ApiKeyRevoked or
        NotificationType.SuspiciousActivity => "Security",

        NotificationType.WebhookDeliveryFailed or
        NotificationType.WebhookDisabled => "Webhook",

        NotificationType.FeatureFlagChanged or
        NotificationType.ConfigurationChanged => "Configuration",

        NotificationType.FeedbackReceived or
        NotificationType.FeedbackResolved => "Feedback",

        _ => "Other"
    };
}

#region Request/Response DTOs

public record SendNotificationApiRequest(
    Guid UserId,
    NotificationType Type,
    string Title,
    string Message,
    NotificationPriority? Priority = null,
    NotificationChannel? Channels = null,
    string? ActionUrl = null,
    string? ActionLabel = null,
    Dictionary<string, object>? Data = null,
    Guid? RelatedEntityId = null,
    string? RelatedEntityType = null,
    Guid? TeamId = null,
    Guid? WorkspaceId = null,
    DateTime? ExpiresAt = null);

public record NotificationResponse(
    Guid Id,
    Guid UserId,
    string Type,
    string Priority,
    string Title,
    string Message,
    string? ActionUrl,
    string? ActionLabel,
    string Status,
    Dictionary<string, object>? Data,
    Guid? RelatedEntityId,
    string? RelatedEntityType,
    Guid? TeamId,
    Guid? WorkspaceId,
    DateTime CreatedAt,
    DateTime? ReadAt,
    DateTime? ArchivedAt,
    DateTime? ExpiresAt);

public record NotificationPageResponse(
    List<NotificationResponse> Items,
    int TotalCount,
    int PageNumber,
    int PageSize,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage);

public record NotificationStatsResponse(
    Guid UserId,
    int TotalCount,
    int UnreadCount,
    int ReadCount,
    int ArchivedCount,
    Dictionary<string, int> CountByType,
    Dictionary<string, int> CountByPriority,
    DateTime? LastNotificationAt,
    DateTime? LastReadAt);

public record MarkAllReadResponse(int Count);

public record SendToGroupResponse(int RecipientCount);

public record NotificationTypeInfo(string Name, string Description, string Category);

#endregion
