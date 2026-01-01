using Carter;
using Microsoft.AspNetCore.Mvc;
using Aegis.Domain.Services;

namespace Aegis.Api.Features.Collaboration;

/// <summary>
/// API endpoints for collaboration features
/// </summary>
public class CollaborationModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/collaboration")
            .WithTags("Collaboration");

        // Workspace endpoints
        group.MapPost("/workspaces", CreateWorkspace);
        group.MapGet("/workspaces/{id:guid}", GetWorkspace);
        group.MapGet("/workspaces", GetUserWorkspaces);
        group.MapDelete("/workspaces/{id:guid}", DeleteWorkspace);
        group.MapPost("/workspaces/{id:guid}/members", AddWorkspaceMember);
        group.MapDelete("/workspaces/{id:guid}/members/{userId:guid}", RemoveWorkspaceMember);
        group.MapPut("/workspaces/{id:guid}/members/{userId:guid}/role", UpdateMemberRole);
        group.MapGet("/workspaces/{id:guid}/members", GetWorkspaceMembers);
        group.MapPost("/workspaces/{id:guid}/transfer", TransferOwnership);

        // Share endpoints
        group.MapPost("/shares", ShareResource);
        group.MapPut("/shares/{id:guid}", UpdateShare);
        group.MapDelete("/shares/{id:guid}", RevokeShare);
        group.MapGet("/shares/{id:guid}", GetShare);
        group.MapGet("/shares/resource/{resourceId:guid}", GetResourceShares);
        group.MapGet("/shares/user/{userId:guid}", GetUserShares);
        group.MapGet("/shares/check-access", CheckAccess);

        // Shareable link endpoints
        group.MapPost("/links", CreateShareableLink);
        group.MapGet("/links/{token}", GetShareableLink);
        group.MapDelete("/links/{id:guid}", RevokeShareableLink);

        // Statistics
        group.MapGet("/stats", GetCollaborationStats);
    }

    private static async Task<IResult> CreateWorkspace(
        [FromBody] CreateWorkspaceRequest request,
        ICollaborationService service,
        CancellationToken ct)
    {
        var result = await service.CreateWorkspaceAsync(request, ct);
        return result.IsSuccess
            ? Results.Created($"/api/collaboration/workspaces/{result.Value.Id}", result.Value)
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> GetWorkspace(
        Guid id,
        ICollaborationService service,
        CancellationToken ct)
    {
        var result = await service.GetWorkspaceByIdAsync(id, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.NotFound(result.Error);
    }

    private static async Task<IResult> GetUserWorkspaces(
        [FromQuery] Guid userId,
        ICollaborationService service,
        CancellationToken ct)
    {
        var result = await service.GetWorkspacesForUserAsync(userId, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> DeleteWorkspace(
        Guid id,
        [FromQuery] Guid deletedBy,
        ICollaborationService service,
        CancellationToken ct)
    {
        var result = await service.DeleteWorkspaceAsync(id, deletedBy, ct);
        return result.IsSuccess
            ? Results.NoContent()
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> AddWorkspaceMember(
        Guid id,
        [FromBody] AddWorkspaceMemberRequest request,
        ICollaborationService service,
        CancellationToken ct)
    {
        var result = await service.AddWorkspaceMemberAsync(id, request, ct);
        return result.IsSuccess
            ? Results.Created($"/api/collaboration/workspaces/{id}/members/{result.Value.UserId}", result.Value)
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> RemoveWorkspaceMember(
        Guid id,
        Guid userId,
        [FromQuery] Guid removedBy,
        ICollaborationService service,
        CancellationToken ct)
    {
        var result = await service.RemoveWorkspaceMemberAsync(id, userId, removedBy, ct);
        return result.IsSuccess
            ? Results.NoContent()
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> UpdateMemberRole(
        Guid id,
        Guid userId,
        [FromBody] UpdateMemberRoleRequest request,
        ICollaborationService service,
        CancellationToken ct)
    {
        var result = await service.UpdateMemberRoleAsync(id, userId, request.NewRole, request.UpdatedBy, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> GetWorkspaceMembers(
        Guid id,
        ICollaborationService service,
        CancellationToken ct)
    {
        var result = await service.GetWorkspaceMembersAsync(id, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.NotFound(result.Error);
    }

    private static async Task<IResult> TransferOwnership(
        Guid id,
        [FromBody] TransferOwnershipRequest request,
        ICollaborationService service,
        CancellationToken ct)
    {
        var result = await service.TransferOwnershipAsync(id, request.NewOwnerId, request.CurrentOwnerId, ct);
        return result.IsSuccess
            ? Results.NoContent()
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> ShareResource(
        [FromBody] ShareResourceRequest request,
        ICollaborationService service,
        CancellationToken ct)
    {
        var result = await service.ShareResourceAsync(request, ct);
        return result.IsSuccess
            ? Results.Created($"/api/collaboration/shares/{result.Value.Id}", result.Value)
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> UpdateShare(
        Guid id,
        [FromBody] UpdateShareRequest request,
        ICollaborationService service,
        CancellationToken ct)
    {
        var result = await service.UpdateShareAsync(id, request, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> RevokeShare(
        Guid id,
        [FromQuery] Guid revokedBy,
        ICollaborationService service,
        CancellationToken ct)
    {
        var result = await service.RevokeShareAsync(id, revokedBy, ct);
        return result.IsSuccess
            ? Results.NoContent()
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> GetShare(
        Guid id,
        ICollaborationService service,
        CancellationToken ct)
    {
        var result = await service.GetShareByIdAsync(id, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.NotFound(result.Error);
    }

    private static async Task<IResult> GetResourceShares(
        Guid resourceId,
        [FromQuery] ShareableResourceType resourceType,
        ICollaborationService service,
        CancellationToken ct)
    {
        var result = await service.GetSharesForResourceAsync(resourceId, resourceType, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> GetUserShares(
        Guid userId,
        [AsParameters] ShareFilter? filter,
        ICollaborationService service,
        CancellationToken ct)
    {
        var result = await service.GetSharesForUserAsync(userId, filter, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> CheckAccess(
        [FromQuery] Guid userId,
        [FromQuery] Guid resourceId,
        [FromQuery] ShareableResourceType resourceType,
        [FromQuery] SharePermission requiredPermission,
        ICollaborationService service,
        CancellationToken ct)
    {
        var result = await service.CheckAccessAsync(userId, resourceId, resourceType, requiredPermission, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> CreateShareableLink(
        [FromBody] CreateShareableLinkRequest request,
        ICollaborationService service,
        CancellationToken ct)
    {
        var result = await service.CreateShareableLinkAsync(request, ct);
        return result.IsSuccess
            ? Results.Created($"/api/collaboration/links/{result.Value.Token}", result.Value)
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> GetShareableLink(
        string token,
        ICollaborationService service,
        CancellationToken ct)
    {
        var result = await service.GetShareableLinkAsync(token, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.NotFound(result.Error);
    }

    private static async Task<IResult> RevokeShareableLink(
        Guid id,
        [FromQuery] Guid revokedBy,
        ICollaborationService service,
        CancellationToken ct)
    {
        var result = await service.RevokeShareableLinkAsync(id, revokedBy, ct);
        return result.IsSuccess
            ? Results.NoContent()
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> GetCollaborationStats(
        [FromQuery] Guid? workspaceId,
        ICollaborationService service,
        CancellationToken ct)
    {
        var result = await service.GetStatsAsync(workspaceId, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(result.Error);
    }
}

/// <summary>
/// Request to update a member's role
/// </summary>
public record UpdateMemberRoleRequest(WorkspaceRole NewRole, Guid UpdatedBy);

/// <summary>
/// Request to transfer ownership
/// </summary>
public record TransferOwnershipRequest(Guid NewOwnerId, Guid CurrentOwnerId);

/// <summary>
/// API endpoints for comments
/// </summary>
public class CommentModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/comments")
            .WithTags("Comments");

        group.MapPost("/", CreateComment);
        group.MapPut("/{id:guid}", UpdateComment);
        group.MapDelete("/{id:guid}", DeleteComment);
        group.MapGet("/{id:guid}", GetComment);
        group.MapGet("/resource/{resourceId:guid}", GetResourceComments);
        group.MapGet("/{id:guid}/replies", GetReplies);
        group.MapPost("/{id:guid}/reactions", AddReaction);
        group.MapDelete("/{id:guid}/reactions", RemoveReaction);
        group.MapGet("/{id:guid}/reactions", GetReactions);
        group.MapPost("/{id:guid}/resolve", ResolveComment);
        group.MapPost("/{id:guid}/reopen", ReopenComment);
        group.MapPost("/{id:guid}/pin", PinComment);
        group.MapPost("/{id:guid}/unpin", UnpinComment);
        group.MapGet("/mentions/{userId:guid}", GetMentions);
        group.MapPost("/mentions/{userId:guid}/read", MarkMentionsAsRead);
        group.MapGet("/search", SearchComments);
        group.MapGet("/stats", GetCommentStats);
    }

    private static async Task<IResult> CreateComment(
        [FromBody] CreateCommentRequest request,
        ICommentService service,
        CancellationToken ct)
    {
        var result = await service.CreateCommentAsync(request, ct);
        return result.IsSuccess
            ? Results.Created($"/api/comments/{result.Value.Id}", result.Value)
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> UpdateComment(
        Guid id,
        [FromBody] UpdateCommentRequest request,
        ICommentService service,
        CancellationToken ct)
    {
        var result = await service.UpdateCommentAsync(id, request, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> DeleteComment(
        Guid id,
        [FromQuery] Guid deletedBy,
        ICommentService service,
        CancellationToken ct)
    {
        var result = await service.DeleteCommentAsync(id, deletedBy, ct);
        return result.IsSuccess
            ? Results.NoContent()
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> GetComment(
        Guid id,
        ICommentService service,
        CancellationToken ct)
    {
        var result = await service.GetCommentByIdAsync(id, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.NotFound(result.Error);
    }

    private static async Task<IResult> GetResourceComments(
        Guid resourceId,
        [FromQuery] CommentableResourceType resourceType,
        [AsParameters] CommentFilter? filter,
        ICommentService service,
        CancellationToken ct)
    {
        var result = await service.GetCommentsForResourceAsync(resourceId, resourceType, filter, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> GetReplies(
        Guid id,
        ICommentService service,
        CancellationToken ct)
    {
        var result = await service.GetRepliesAsync(id, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.NotFound(result.Error);
    }

    private static async Task<IResult> AddReaction(
        Guid id,
        [FromBody] AddReactionRequest request,
        ICommentService service,
        CancellationToken ct)
    {
        var result = await service.AddReactionAsync(id, request, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> RemoveReaction(
        Guid id,
        [FromQuery] Guid userId,
        [FromQuery] ReactionType reactionType,
        ICommentService service,
        CancellationToken ct)
    {
        var result = await service.RemoveReactionAsync(id, userId, reactionType, ct);
        return result.IsSuccess
            ? Results.NoContent()
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> GetReactions(
        Guid id,
        ICommentService service,
        CancellationToken ct)
    {
        var result = await service.GetReactionsAsync(id, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.NotFound(result.Error);
    }

    private static async Task<IResult> ResolveComment(
        Guid id,
        [FromQuery] Guid resolvedBy,
        ICommentService service,
        CancellationToken ct)
    {
        var result = await service.ResolveCommentAsync(id, resolvedBy, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> ReopenComment(
        Guid id,
        [FromQuery] Guid reopenedBy,
        ICommentService service,
        CancellationToken ct)
    {
        var result = await service.ReopenCommentAsync(id, reopenedBy, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> PinComment(
        Guid id,
        [FromQuery] Guid pinnedBy,
        ICommentService service,
        CancellationToken ct)
    {
        var result = await service.PinCommentAsync(id, pinnedBy, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> UnpinComment(
        Guid id,
        [FromQuery] Guid unpinnedBy,
        ICommentService service,
        CancellationToken ct)
    {
        var result = await service.UnpinCommentAsync(id, unpinnedBy, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> GetMentions(
        Guid userId,
        [AsParameters] MentionFilter? filter,
        ICommentService service,
        CancellationToken ct)
    {
        var result = await service.GetMentionsForUserAsync(userId, filter, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> MarkMentionsAsRead(
        Guid userId,
        [FromBody] MarkMentionsReadRequest? request,
        ICommentService service,
        CancellationToken ct)
    {
        var result = await service.MarkMentionsAsReadAsync(userId, request?.CommentIds, ct);
        return result.IsSuccess
            ? Results.NoContent()
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> SearchComments(
        [AsParameters] CommentSearchRequest request,
        ICommentService service,
        CancellationToken ct)
    {
        var result = await service.SearchCommentsAsync(request, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> GetCommentStats(
        [FromQuery] Guid? resourceId,
        [FromQuery] CommentableResourceType? resourceType,
        ICommentService service,
        CancellationToken ct)
    {
        var result = await service.GetStatsAsync(resourceId, resourceType, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(result.Error);
    }
}

/// <summary>
/// Request to mark mentions as read
/// </summary>
public record MarkMentionsReadRequest(List<Guid>? CommentIds);

/// <summary>
/// API endpoints for activity feed
/// </summary>
public class ActivityFeedModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/activity")
            .WithTags("Activity Feed");

        group.MapPost("/", RecordActivity);
        group.MapGet("/{id:guid}", GetActivity);
        group.MapGet("/workspace/{workspaceId:guid}", GetWorkspaceActivities);
        group.MapGet("/resource/{resourceId:guid}", GetResourceActivities);
        group.MapGet("/user/{userId:guid}", GetUserActivities);
        group.MapGet("/feed/{userId:guid}", GetPersonalizedFeed);
        group.MapGet("/unseen-count/{userId:guid}", GetUnseenCount);
        group.MapPost("/seen/{userId:guid}", MarkAsSeen);
        group.MapPost("/subscriptions", Subscribe);
        group.MapDelete("/subscriptions/{id:guid}", Unsubscribe);
        group.MapGet("/subscriptions/{userId:guid}", GetSubscriptions);
        group.MapGet("/aggregated/{workspaceId:guid}", GetAggregatedActivities);
        group.MapGet("/stats", GetActivityStats);
    }

    private static async Task<IResult> RecordActivity(
        [FromBody] RecordActivityRequest request,
        IActivityFeedService service,
        CancellationToken ct)
    {
        var result = await service.RecordActivityAsync(request, ct);
        return result.IsSuccess
            ? Results.Created($"/api/activity/{result.Value.Id}", result.Value)
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> GetActivity(
        Guid id,
        IActivityFeedService service,
        CancellationToken ct)
    {
        var result = await service.GetActivityByIdAsync(id, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.NotFound(result.Error);
    }

    private static async Task<IResult> GetWorkspaceActivities(
        Guid workspaceId,
        [AsParameters] ActivityFilter? filter,
        IActivityFeedService service,
        CancellationToken ct)
    {
        var result = await service.GetWorkspaceActivitiesAsync(workspaceId, filter, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> GetResourceActivities(
        Guid resourceId,
        [FromQuery] ActivityResourceType resourceType,
        [AsParameters] ActivityFilter? filter,
        IActivityFeedService service,
        CancellationToken ct)
    {
        var result = await service.GetResourceActivitiesAsync(resourceId, resourceType, filter, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> GetUserActivities(
        Guid userId,
        [AsParameters] ActivityFilter? filter,
        IActivityFeedService service,
        CancellationToken ct)
    {
        var result = await service.GetUserActivitiesAsync(userId, filter, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> GetPersonalizedFeed(
        Guid userId,
        [AsParameters] PersonalizedFeedOptions? options,
        IActivityFeedService service,
        CancellationToken ct)
    {
        var result = await service.GetPersonalizedFeedAsync(userId, options, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> GetUnseenCount(
        Guid userId,
        [FromQuery] Guid? workspaceId,
        IActivityFeedService service,
        CancellationToken ct)
    {
        var result = await service.GetUnseenCountAsync(userId, workspaceId, ct);
        return result.IsSuccess
            ? Results.Ok(new { UnseenCount = result.Value })
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> MarkAsSeen(
        Guid userId,
        [FromBody] MarkSeenRequest? request,
        IActivityFeedService service,
        CancellationToken ct)
    {
        var result = await service.MarkAsSeenAsync(userId, request?.ActivityIds, ct);
        return result.IsSuccess
            ? Results.NoContent()
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> Subscribe(
        [FromBody] SubscribeToActivityRequest request,
        IActivityFeedService service,
        CancellationToken ct)
    {
        var result = await service.SubscribeAsync(request, ct);
        return result.IsSuccess
            ? Results.Created($"/api/activity/subscriptions/{result.Value.Id}", result.Value)
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> Unsubscribe(
        Guid id,
        IActivityFeedService service,
        CancellationToken ct)
    {
        var result = await service.UnsubscribeAsync(id, ct);
        return result.IsSuccess
            ? Results.NoContent()
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> GetSubscriptions(
        Guid userId,
        IActivityFeedService service,
        CancellationToken ct)
    {
        var result = await service.GetSubscriptionsAsync(userId, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> GetAggregatedActivities(
        Guid workspaceId,
        [AsParameters] AggregationOptions options,
        IActivityFeedService service,
        CancellationToken ct)
    {
        var result = await service.GetAggregatedActivitiesAsync(workspaceId, options, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> GetActivityStats(
        [FromQuery] Guid? workspaceId,
        [FromQuery] DateTime? since,
        IActivityFeedService service,
        CancellationToken ct)
    {
        var result = await service.GetStatsAsync(workspaceId, since, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(result.Error);
    }
}

/// <summary>
/// Request to mark activities as seen
/// </summary>
public record MarkSeenRequest(List<Guid>? ActivityIds);

/// <summary>
/// API endpoints for presence
/// </summary>
public class PresenceModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/presence")
            .WithTags("Presence");

        group.MapPost("/join", JoinResource);
        group.MapPost("/leave", LeaveResource);
        group.MapPut("/status", UpdateStatus);
        group.MapGet("/resource/{resourceId:guid}", GetResourcePresence);
        group.MapGet("/workspace/{workspaceId:guid}", GetWorkspacePresence);
        group.MapGet("/user/{userId:guid}", GetUserPresence);
        group.MapPut("/cursor", UpdateCursor);
        group.MapGet("/cursors/{resourceId:guid}", GetCursors);
        group.MapGet("/stats", GetPresenceStats);
    }

    private static async Task<IResult> JoinResource(
        [FromBody] JoinResourceRequest request,
        IPresenceService service,
        CancellationToken ct)
    {
        var result = await service.JoinResourceAsync(request, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> LeaveResource(
        [FromBody] LeaveResourceRequest request,
        IPresenceService service,
        CancellationToken ct)
    {
        var result = await service.LeaveResourceAsync(request.UserId, request.ResourceId, request.ConnectionId, ct);
        return result.IsSuccess
            ? Results.NoContent()
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> UpdateStatus(
        [FromBody] SetStatusRequest request,
        IPresenceService service,
        CancellationToken ct)
    {
        var result = await service.SetStatusAsync(request.UserId, request.Status, request.StatusMessage, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> GetResourcePresence(
        Guid resourceId,
        IPresenceService service,
        CancellationToken ct)
    {
        var result = await service.GetResourcePresenceAsync(resourceId, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> GetWorkspacePresence(
        Guid workspaceId,
        IPresenceService service,
        CancellationToken ct)
    {
        var result = await service.GetWorkspacePresenceAsync(workspaceId, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> GetUserPresence(
        Guid userId,
        IPresenceService service,
        CancellationToken ct)
    {
        var result = await service.GetUserPresenceAsync(userId, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> UpdateCursor(
        [FromBody] UpdateCursorRequest request,
        IPresenceService service,
        CancellationToken ct)
    {
        var result = await service.UpdateCursorPositionAsync(request.UserId, request.ResourceId, request.Position, ct);
        return result.IsSuccess
            ? Results.NoContent()
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> GetCursors(
        Guid resourceId,
        IPresenceService service,
        CancellationToken ct)
    {
        var result = await service.GetCursorPositionsAsync(resourceId, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> GetPresenceStats(
        [FromQuery] Guid? workspaceId,
        IPresenceService service,
        CancellationToken ct)
    {
        var result = await service.GetStatsAsync(workspaceId, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.BadRequest(result.Error);
    }
}

/// <summary>
/// Request to leave a resource
/// </summary>
public record LeaveResourceRequest(Guid UserId, Guid ResourceId, string? ConnectionId);

/// <summary>
/// Request to set status
/// </summary>
public record SetStatusRequest(Guid UserId, PresenceStatus Status, string? StatusMessage);

/// <summary>
/// Request to update cursor
/// </summary>
public record UpdateCursorRequest(Guid UserId, Guid ResourceId, CursorPosition Position);
