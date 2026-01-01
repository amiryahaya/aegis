using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Service for tracking real-time user presence and activity
/// </summary>
public interface IPresenceService
{
    /// <summary>
    /// Registers a user's presence on a resource
    /// </summary>
    Task<Result<UserPresence>> JoinResourceAsync(
        JoinResourceRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a user's presence from a resource
    /// </summary>
    Task<Result> LeaveResourceAsync(
        Guid userId,
        Guid resourceId,
        string? connectionId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a user's presence state
    /// </summary>
    Task<Result<UserPresence>> UpdatePresenceAsync(
        Guid userId,
        UpdatePresenceRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all users currently viewing a resource
    /// </summary>
    Task<Result<IReadOnlyList<UserPresence>>> GetResourcePresenceAsync(
        Guid resourceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user's presence across all resources
    /// </summary>
    Task<Result<IReadOnlyList<UserPresence>>> GetUserPresenceAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets online users in a workspace
    /// </summary>
    Task<Result<IReadOnlyList<UserPresence>>> GetWorkspacePresenceAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates user's cursor/selection position (for collaborative editing)
    /// </summary>
    Task<Result> UpdateCursorPositionAsync(
        Guid userId,
        Guid resourceId,
        CursorPosition position,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all cursor positions for a resource
    /// </summary>
    Task<Result<IReadOnlyList<UserCursor>>> GetCursorPositionsAsync(
        Guid resourceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets user's activity status
    /// </summary>
    Task<Result<UserPresence>> SetStatusAsync(
        Guid userId,
        PresenceStatus status,
        string? statusMessage = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles user disconnection (cleanup stale presence)
    /// </summary>
    Task<Result> HandleDisconnectAsync(
        string connectionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up stale presence records
    /// </summary>
    Task<Result<int>> CleanupStalePresenceAsync(
        TimeSpan staleThreshold,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets presence statistics
    /// </summary>
    Task<Result<PresenceStats>> GetStatsAsync(
        Guid? workspaceId = null,
        CancellationToken cancellationToken = default);
}

#region Presence Types

/// <summary>
/// A user's presence on a resource
/// </summary>
public record UserPresence
{
    public Guid UserId { get; init; }
    public required string DisplayName { get; init; }
    public string? AvatarUrl { get; init; }
    public string? Email { get; init; }
    public Guid? ResourceId { get; init; }
    public PresenceResourceType? ResourceType { get; init; }
    public Guid? WorkspaceId { get; init; }
    public string? ConnectionId { get; init; }
    public PresenceStatus Status { get; init; } = PresenceStatus.Online;
    public string? StatusMessage { get; init; }
    public PresenceActivity Activity { get; init; } = PresenceActivity.Viewing;
    public DateTime JoinedAt { get; init; } = DateTime.UtcNow;
    public DateTime LastActiveAt { get; init; } = DateTime.UtcNow;
    public CursorPosition? CursorPosition { get; init; }
    public string? Color { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = new();
}

public enum PresenceResourceType
{
    Session,
    Document,
    Query,
    Report,
    Dashboard,
    Workspace,
    Collection
}

public enum PresenceStatus
{
    Online,
    Away,
    Busy,
    DoNotDisturb,
    Offline
}

public enum PresenceActivity
{
    Viewing,
    Editing,
    Commenting,
    Typing,
    Idle
}

/// <summary>
/// Request to join a resource
/// </summary>
public record JoinResourceRequest
{
    public Guid UserId { get; init; }
    public required string DisplayName { get; init; }
    public string? AvatarUrl { get; init; }
    public string? Email { get; init; }
    public Guid ResourceId { get; init; }
    public PresenceResourceType ResourceType { get; init; }
    public Guid? WorkspaceId { get; init; }
    public string? ConnectionId { get; init; }
    public PresenceActivity InitialActivity { get; init; } = PresenceActivity.Viewing;
}

/// <summary>
/// Request to update presence
/// </summary>
public record UpdatePresenceRequest
{
    public Guid? ResourceId { get; init; }
    public PresenceStatus? Status { get; init; }
    public string? StatusMessage { get; init; }
    public PresenceActivity? Activity { get; init; }
}

#endregion

#region Cursor Types

/// <summary>
/// A user's cursor position
/// </summary>
public record CursorPosition
{
    public int? Line { get; init; }
    public int? Column { get; init; }
    public int? StartOffset { get; init; }
    public int? EndOffset { get; init; }
    public string? SelectionText { get; init; }
    public string? ElementId { get; init; }
    public double? X { get; init; }
    public double? Y { get; init; }
}

/// <summary>
/// A user's cursor with identity
/// </summary>
public record UserCursor
{
    public Guid UserId { get; init; }
    public required string DisplayName { get; init; }
    public string? AvatarUrl { get; init; }
    public required string Color { get; init; }
    public required CursorPosition Position { get; init; }
    public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;
}

#endregion

#region Statistics

/// <summary>
/// Presence statistics
/// </summary>
public record PresenceStats
{
    public int TotalOnlineUsers { get; init; }
    public int TotalActiveResources { get; init; }
    public Dictionary<PresenceStatus, int> UsersByStatus { get; init; } = new();
    public Dictionary<PresenceActivity, int> UsersByActivity { get; init; } = new();
    public Dictionary<PresenceResourceType, int> UsersByResourceType { get; init; } = new();
    public int PeakConcurrentUsers { get; init; }
    public DateTime PeakTime { get; init; }
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
}

#endregion
