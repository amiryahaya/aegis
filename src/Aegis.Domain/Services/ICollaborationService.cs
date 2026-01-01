using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Service for managing workspace collaboration and sharing
/// </summary>
public interface ICollaborationService
{
    /// <summary>
    /// Shares a resource with a user or team
    /// </summary>
    Task<Result<Share>> ShareResourceAsync(
        ShareResourceRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates sharing permissions for an existing share
    /// </summary>
    Task<Result<Share>> UpdateShareAsync(
        Guid shareId,
        UpdateShareRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes a share
    /// </summary>
    Task<Result> RevokeShareAsync(
        Guid shareId,
        Guid revokedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a share by ID
    /// </summary>
    Task<Result<Share>> GetShareByIdAsync(
        Guid shareId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all shares for a resource
    /// </summary>
    Task<Result<IReadOnlyList<Share>>> GetSharesForResourceAsync(
        Guid resourceId,
        ShareableResourceType resourceType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all shares accessible by a user
    /// </summary>
    Task<Result<SharePage>> GetSharesForUserAsync(
        Guid userId,
        ShareFilter? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user has access to a resource
    /// </summary>
    Task<Result<AccessCheckResult>> CheckAccessAsync(
        Guid userId,
        Guid resourceId,
        ShareableResourceType resourceType,
        SharePermission requiredPermission,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a shareable link for a resource
    /// </summary>
    Task<Result<ShareableLink>> CreateShareableLinkAsync(
        CreateShareableLinkRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a resource by shareable link token
    /// </summary>
    Task<Result<ShareableLink>> GetShareableLinkAsync(
        string token,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes a shareable link
    /// </summary>
    Task<Result> RevokeShareableLinkAsync(
        Guid linkId,
        Guid revokedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or joins a workspace
    /// </summary>
    Task<Result<CollaborationWorkspace>> CreateWorkspaceAsync(
        CreateWorkspaceRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a workspace by ID
    /// </summary>
    Task<Result<CollaborationWorkspace>> GetWorkspaceByIdAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets workspaces for a user
    /// </summary>
    Task<Result<IReadOnlyList<CollaborationWorkspace>>> GetWorkspacesForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a member to a workspace
    /// </summary>
    Task<Result<WorkspaceMember>> AddWorkspaceMemberAsync(
        Guid workspaceId,
        AddWorkspaceMemberRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a member from a workspace
    /// </summary>
    Task<Result> RemoveWorkspaceMemberAsync(
        Guid workspaceId,
        Guid userId,
        Guid removedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a member's role in a workspace
    /// </summary>
    Task<Result<WorkspaceMember>> UpdateMemberRoleAsync(
        Guid workspaceId,
        Guid userId,
        WorkspaceRole newRole,
        Guid updatedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets members of a workspace
    /// </summary>
    Task<Result<IReadOnlyList<WorkspaceMember>>> GetWorkspaceMembersAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transfers ownership of a workspace
    /// </summary>
    Task<Result> TransferOwnershipAsync(
        Guid workspaceId,
        Guid newOwnerId,
        Guid currentOwnerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a workspace
    /// </summary>
    Task<Result> DeleteWorkspaceAsync(
        Guid workspaceId,
        Guid deletedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets collaboration statistics
    /// </summary>
    Task<Result<CollaborationStats>> GetStatsAsync(
        Guid? workspaceId = null,
        CancellationToken cancellationToken = default);
}

#region Share Types

/// <summary>
/// A share grants access to a resource
/// </summary>
public record Share
{
    public Guid Id { get; init; }
    public Guid ResourceId { get; init; }
    public required ShareableResourceType ResourceType { get; init; }
    public Guid SharedBy { get; init; }
    public required ShareTarget Target { get; init; }
    public SharePermission Permission { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; init; }
    public bool IsActive { get; init; } = true;
    public string? Message { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = new();
}

/// <summary>
/// Target of a share (user, team, or workspace)
/// </summary>
public record ShareTarget
{
    public required ShareTargetType Type { get; init; }
    public Guid? UserId { get; init; }
    public Guid? TeamId { get; init; }
    public Guid? WorkspaceId { get; init; }
    public string? Email { get; init; }
}

public enum ShareTargetType
{
    User,
    Team,
    Workspace,
    Email,
    Anyone
}

public enum ShareableResourceType
{
    Session,
    Document,
    DataSource,
    Query,
    Report,
    Dashboard,
    Workspace,
    Collection
}

public enum SharePermission
{
    View = 1,
    Comment = 2,
    Edit = 3,
    Admin = 4,
    Owner = 5
}

/// <summary>
/// Request to share a resource
/// </summary>
public record ShareResourceRequest
{
    public Guid ResourceId { get; init; }
    public required ShareableResourceType ResourceType { get; init; }
    public Guid SharedBy { get; init; }
    public required ShareTarget Target { get; init; }
    public SharePermission Permission { get; init; } = SharePermission.View;
    public DateTime? ExpiresAt { get; init; }
    public string? Message { get; init; }
    public bool NotifyTarget { get; init; } = true;
}

/// <summary>
/// Request to update a share
/// </summary>
public record UpdateShareRequest
{
    public SharePermission? Permission { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public Guid UpdatedBy { get; init; }
}

/// <summary>
/// Filter for listing shares
/// </summary>
public record ShareFilter
{
    public ShareableResourceType? ResourceType { get; init; }
    public SharePermission? MinPermission { get; init; }
    public bool? IncludeExpired { get; init; }
    public Guid? SharedBy { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

/// <summary>
/// Paginated shares result
/// </summary>
public record SharePage
{
    public IReadOnlyList<Share> Shares { get; init; } = Array.Empty<Share>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public bool HasMore => Page * PageSize < TotalCount;
}

/// <summary>
/// Result of an access check
/// </summary>
public record AccessCheckResult
{
    public bool HasAccess { get; init; }
    public SharePermission? GrantedPermission { get; init; }
    public Guid? ShareId { get; init; }
    public string? Reason { get; init; }
}

#endregion

#region Shareable Links

/// <summary>
/// A shareable link provides access via token
/// </summary>
public record ShareableLink
{
    public Guid Id { get; init; }
    public Guid ResourceId { get; init; }
    public required ShareableResourceType ResourceType { get; init; }
    public required string Token { get; init; }
    public Guid CreatedBy { get; init; }
    public SharePermission Permission { get; init; } = SharePermission.View;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; init; }
    public int? MaxUses { get; init; }
    public int UseCount { get; init; }
    public bool IsActive { get; init; } = true;
    public bool RequiresAuthentication { get; init; }
    public string? Password { get; init; }
}

/// <summary>
/// Request to create a shareable link
/// </summary>
public record CreateShareableLinkRequest
{
    public Guid ResourceId { get; init; }
    public required ShareableResourceType ResourceType { get; init; }
    public Guid CreatedBy { get; init; }
    public SharePermission Permission { get; init; } = SharePermission.View;
    public DateTime? ExpiresAt { get; init; }
    public int? MaxUses { get; init; }
    public bool RequiresAuthentication { get; init; }
    public string? Password { get; init; }
}

#endregion

#region Workspace Types

/// <summary>
/// A workspace for team collaboration
/// </summary>
public record CollaborationWorkspace
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public Guid OwnerId { get; init; }
    public CollaborationWorkspaceType Type { get; init; } = CollaborationWorkspaceType.Team;
    public CollaborationWorkspaceVisibility Visibility { get; init; } = CollaborationWorkspaceVisibility.Private;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;
    public bool IsActive { get; init; } = true;
    public CollaborationWorkspaceSettings Settings { get; init; } = new();
    public Dictionary<string, object> Metadata { get; init; } = new();
    public List<WorkspaceMember> Members { get; init; } = new();
}

public enum CollaborationWorkspaceType
{
    Personal,
    Team,
    Project,
    Department,
    Organization
}

public enum CollaborationWorkspaceVisibility
{
    Private,
    Internal,
    Public
}

/// <summary>
/// Workspace settings
/// </summary>
public record CollaborationWorkspaceSettings
{
    public bool AllowGuestAccess { get; init; }
    public bool RequireApprovalForJoin { get; init; }
    public SharePermission DefaultMemberPermission { get; init; } = SharePermission.Edit;
    public int MaxMembers { get; init; } = 100;
    public bool EnableComments { get; init; } = true;
    public bool EnableActivityFeed { get; init; } = true;
    public bool EnableVersionHistory { get; init; } = true;
    public int RetentionDays { get; init; } = 365;
}

/// <summary>
/// Request to create a workspace
/// </summary>
public record CreateWorkspaceRequest
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public Guid OwnerId { get; init; }
    public CollaborationWorkspaceType Type { get; init; } = CollaborationWorkspaceType.Team;
    public CollaborationWorkspaceVisibility Visibility { get; init; } = CollaborationWorkspaceVisibility.Private;
    public CollaborationWorkspaceSettings? Settings { get; init; }
}

/// <summary>
/// A member of a workspace
/// </summary>
public record WorkspaceMember
{
    public Guid UserId { get; init; }
    public Guid WorkspaceId { get; init; }
    public required string DisplayName { get; init; }
    public string? Email { get; init; }
    public string? AvatarUrl { get; init; }
    public WorkspaceRole Role { get; init; } = WorkspaceRole.Member;
    public DateTime JoinedAt { get; init; } = DateTime.UtcNow;
    public DateTime? LastActiveAt { get; init; }
    public MemberStatus Status { get; init; } = MemberStatus.Active;
}

public enum WorkspaceRole
{
    Guest = 1,
    Viewer = 2,
    Member = 3,
    Admin = 4,
    Owner = 5
}

public enum MemberStatus
{
    Pending,
    Active,
    Inactive,
    Suspended
}

/// <summary>
/// Request to add a workspace member
/// </summary>
public record AddWorkspaceMemberRequest
{
    public Guid UserId { get; init; }
    public required string DisplayName { get; init; }
    public string? Email { get; init; }
    public WorkspaceRole Role { get; init; } = WorkspaceRole.Member;
    public Guid AddedBy { get; init; }
}

#endregion

#region Statistics

/// <summary>
/// Collaboration statistics
/// </summary>
public record CollaborationStats
{
    public int TotalWorkspaces { get; init; }
    public int ActiveWorkspaces { get; init; }
    public int TotalShares { get; init; }
    public int ActiveShares { get; init; }
    public int TotalShareableLinks { get; init; }
    public int ActiveShareableLinks { get; init; }
    public int TotalMembers { get; init; }
    public Dictionary<ShareableResourceType, int> SharesByResourceType { get; init; } = new();
    public Dictionary<SharePermission, int> SharesByPermission { get; init; } = new();
    public Dictionary<CollaborationWorkspaceType, int> WorkspacesByType { get; init; } = new();
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
}

#endregion
