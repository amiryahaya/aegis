using System.Collections.Concurrent;
using Aegis.Domain.Common;
using Aegis.Domain.Services;

namespace Aegis.Infrastructure.Services.Collaboration;

/// <summary>
/// In-memory implementation of presence tracking service
/// </summary>
public class InMemoryPresenceService : IPresenceService
{
    private readonly ConcurrentDictionary<(Guid UserId, Guid ResourceId), UserPresence> _resourcePresence = new();
    private readonly ConcurrentDictionary<Guid, UserPresence> _userStatus = new();
    private readonly ConcurrentDictionary<string, Guid> _connectionIndex = new();
    private readonly ConcurrentDictionary<(Guid UserId, Guid ResourceId), UserCursor> _cursors = new();

    private static readonly string[] _colors = new[]
    {
        "#FF6B6B", "#4ECDC4", "#45B7D1", "#96CEB4", "#FFEAA7",
        "#DDA0DD", "#98D8C8", "#F7DC6F", "#BB8FCE", "#85C1E9"
    };

    private int _peakConcurrentUsers;
    private DateTime _peakTime = DateTime.UtcNow;

    public Task<Result<UserPresence>> JoinResourceAsync(
        JoinResourceRequest request,
        CancellationToken cancellationToken = default)
    {
        var color = GetColorForUser(request.UserId);

        var presence = new UserPresence
        {
            UserId = request.UserId,
            DisplayName = request.DisplayName,
            AvatarUrl = request.AvatarUrl,
            Email = request.Email,
            ResourceId = request.ResourceId,
            ResourceType = request.ResourceType,
            WorkspaceId = request.WorkspaceId,
            ConnectionId = request.ConnectionId,
            Status = PresenceStatus.Online,
            Activity = request.InitialActivity,
            JoinedAt = DateTime.UtcNow,
            LastActiveAt = DateTime.UtcNow,
            Color = color
        };

        var key = (request.UserId, request.ResourceId);
        _resourcePresence[key] = presence;

        // Update user's global status
        _userStatus[request.UserId] = presence;

        // Index by connection ID for disconnect handling
        if (!string.IsNullOrEmpty(request.ConnectionId))
        {
            _connectionIndex[request.ConnectionId] = request.UserId;
        }

        // Track peak users
        var currentOnline = _userStatus.Values.Count(u => u.Status == PresenceStatus.Online);
        if (currentOnline > _peakConcurrentUsers)
        {
            _peakConcurrentUsers = currentOnline;
            _peakTime = DateTime.UtcNow;
        }

        return Task.FromResult(Result<UserPresence>.Success(presence));
    }

    public Task<Result> LeaveResourceAsync(
        Guid userId,
        Guid resourceId,
        string? connectionId = null,
        CancellationToken cancellationToken = default)
    {
        var key = (userId, resourceId);
        _resourcePresence.TryRemove(key, out _);
        _cursors.TryRemove(key, out _);

        // Remove connection index
        if (!string.IsNullOrEmpty(connectionId))
        {
            _connectionIndex.TryRemove(connectionId, out _);
        }

        // Check if user has any other resource presence
        var hasOtherPresence = _resourcePresence.Keys.Any(k => k.UserId == userId);
        if (!hasOtherPresence)
        {
            // Update user status to offline
            if (_userStatus.TryGetValue(userId, out var status))
            {
                _userStatus[userId] = status with { Status = PresenceStatus.Offline };
            }
        }

        return Task.FromResult(Result.Success());
    }

    public Task<Result<UserPresence>> UpdatePresenceAsync(
        Guid userId,
        UpdatePresenceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_userStatus.TryGetValue(userId, out var currentPresence))
        {
            return Task.FromResult(Result<UserPresence>.Failure(
                Error.NotFound("Presence.NotFound", $"No presence found for user {userId}")));
        }

        var updatedPresence = currentPresence with
        {
            Status = request.Status ?? currentPresence.Status,
            StatusMessage = request.StatusMessage ?? currentPresence.StatusMessage,
            Activity = request.Activity ?? currentPresence.Activity,
            LastActiveAt = DateTime.UtcNow
        };

        _userStatus[userId] = updatedPresence;

        // Update resource presence if user is on a resource
        if (request.ResourceId.HasValue)
        {
            var key = (userId, request.ResourceId.Value);
            if (_resourcePresence.TryGetValue(key, out var resourcePresence))
            {
                _resourcePresence[key] = resourcePresence with
                {
                    Status = updatedPresence.Status,
                    StatusMessage = updatedPresence.StatusMessage,
                    Activity = request.Activity ?? resourcePresence.Activity,
                    LastActiveAt = DateTime.UtcNow
                };
            }
        }

        return Task.FromResult(Result<UserPresence>.Success(updatedPresence));
    }

    public Task<Result<IReadOnlyList<UserPresence>>> GetResourcePresenceAsync(
        Guid resourceId,
        CancellationToken cancellationToken = default)
    {
        var presences = _resourcePresence.Values
            .Where(p => p.ResourceId == resourceId && p.Status != PresenceStatus.Offline)
            .OrderByDescending(p => p.Activity == PresenceActivity.Editing)
            .ThenBy(p => p.DisplayName)
            .ToList();

        return Task.FromResult(Result<IReadOnlyList<UserPresence>>.Success(presences));
    }

    public Task<Result<IReadOnlyList<UserPresence>>> GetUserPresenceAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var presences = _resourcePresence.Values
            .Where(p => p.UserId == userId)
            .ToList();

        return Task.FromResult(Result<IReadOnlyList<UserPresence>>.Success(presences));
    }

    public Task<Result<IReadOnlyList<UserPresence>>> GetWorkspacePresenceAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        var presences = _resourcePresence.Values
            .Where(p => p.WorkspaceId == workspaceId && p.Status != PresenceStatus.Offline)
            .GroupBy(p => p.UserId)
            .Select(g => g.OrderByDescending(p => p.LastActiveAt).First())
            .OrderBy(p => p.DisplayName)
            .ToList();

        return Task.FromResult(Result<IReadOnlyList<UserPresence>>.Success(presences));
    }

    public Task<Result> UpdateCursorPositionAsync(
        Guid userId,
        Guid resourceId,
        CursorPosition position,
        CancellationToken cancellationToken = default)
    {
        var presenceKey = (userId, resourceId);
        if (!_resourcePresence.TryGetValue(presenceKey, out var presence))
        {
            return Task.FromResult(Result.Failure(
                Error.NotFound("Presence.NotFound", "User is not present on this resource")));
        }

        var cursor = new UserCursor
        {
            UserId = userId,
            DisplayName = presence.DisplayName,
            AvatarUrl = presence.AvatarUrl,
            Color = presence.Color ?? GetColorForUser(userId),
            Position = position,
            UpdatedAt = DateTime.UtcNow
        };

        _cursors[presenceKey] = cursor;

        // Update presence activity
        _resourcePresence[presenceKey] = presence with
        {
            CursorPosition = position,
            Activity = position.SelectionText != null ? PresenceActivity.Editing : PresenceActivity.Viewing,
            LastActiveAt = DateTime.UtcNow
        };

        return Task.FromResult(Result.Success());
    }

    public Task<Result<IReadOnlyList<UserCursor>>> GetCursorPositionsAsync(
        Guid resourceId,
        CancellationToken cancellationToken = default)
    {
        var cursors = _cursors.Values
            .Where(c => _resourcePresence.ContainsKey((c.UserId, resourceId)))
            .OrderBy(c => c.DisplayName)
            .ToList();

        return Task.FromResult(Result<IReadOnlyList<UserCursor>>.Success(cursors));
    }

    public Task<Result<UserPresence>> SetStatusAsync(
        Guid userId,
        PresenceStatus status,
        string? statusMessage = null,
        CancellationToken cancellationToken = default)
    {
        if (!_userStatus.TryGetValue(userId, out var currentPresence))
        {
            // Create new presence if not exists
            currentPresence = new UserPresence
            {
                UserId = userId,
                DisplayName = "User",
                Status = status,
                StatusMessage = statusMessage,
                LastActiveAt = DateTime.UtcNow,
                Color = GetColorForUser(userId)
            };
        }
        else
        {
            currentPresence = currentPresence with
            {
                Status = status,
                StatusMessage = statusMessage,
                LastActiveAt = DateTime.UtcNow
            };
        }

        _userStatus[userId] = currentPresence;

        // Update all resource presences for this user
        var resourceKeys = _resourcePresence.Keys.Where(k => k.UserId == userId).ToList();
        foreach (var key in resourceKeys)
        {
            if (_resourcePresence.TryGetValue(key, out var resourcePresence))
            {
                _resourcePresence[key] = resourcePresence with
                {
                    Status = status,
                    StatusMessage = statusMessage,
                    LastActiveAt = DateTime.UtcNow
                };
            }
        }

        return Task.FromResult(Result<UserPresence>.Success(currentPresence));
    }

    public Task<Result> HandleDisconnectAsync(
        string connectionId,
        CancellationToken cancellationToken = default)
    {
        if (!_connectionIndex.TryRemove(connectionId, out var userId))
        {
            return Task.FromResult(Result.Success());
        }

        // Remove all resource presences for this user's connection
        var keysToRemove = _resourcePresence
            .Where(kv => kv.Value.UserId == userId && kv.Value.ConnectionId == connectionId)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _resourcePresence.TryRemove(key, out _);
            _cursors.TryRemove(key, out _);
        }

        // Check if user has any other connections
        var hasOtherConnections = _resourcePresence.Values.Any(p => p.UserId == userId);
        if (!hasOtherConnections && _userStatus.TryGetValue(userId, out var status))
        {
            _userStatus[userId] = status with { Status = PresenceStatus.Offline };
        }

        return Task.FromResult(Result.Success());
    }

    public Task<Result<int>> CleanupStalePresenceAsync(
        TimeSpan staleThreshold,
        CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow - staleThreshold;
        var staleKeys = _resourcePresence
            .Where(kv => kv.Value.LastActiveAt < cutoff)
            .Select(kv => kv.Key)
            .ToList();

        var removedCount = 0;
        foreach (var key in staleKeys)
        {
            if (_resourcePresence.TryRemove(key, out _))
            {
                _cursors.TryRemove(key, out _);
                removedCount++;
            }
        }

        // Update user statuses
        var staleUserIds = staleKeys.Select(k => k.UserId).Distinct();
        foreach (var userId in staleUserIds)
        {
            var hasActivePresence = _resourcePresence.Keys.Any(k => k.UserId == userId);
            if (!hasActivePresence && _userStatus.TryGetValue(userId, out var status))
            {
                _userStatus[userId] = status with { Status = PresenceStatus.Offline };
            }
        }

        return Task.FromResult(Result<int>.Success(removedCount));
    }

    public Task<Result<PresenceStats>> GetStatsAsync(
        Guid? workspaceId = null,
        CancellationToken cancellationToken = default)
    {
        var presences = _resourcePresence.Values.AsEnumerable();

        if (workspaceId.HasValue)
        {
            presences = presences.Where(p => p.WorkspaceId == workspaceId);
        }

        var presenceList = presences.ToList();
        var uniqueUsers = presenceList
            .GroupBy(p => p.UserId)
            .Select(g => g.OrderByDescending(p => p.LastActiveAt).First())
            .ToList();

        var stats = new PresenceStats
        {
            TotalOnlineUsers = uniqueUsers.Count(u => u.Status == PresenceStatus.Online),
            TotalActiveResources = presenceList.Select(p => p.ResourceId).Distinct().Count(),
            UsersByStatus = uniqueUsers
                .GroupBy(u => u.Status)
                .ToDictionary(g => g.Key, g => g.Count()),
            UsersByActivity = presenceList
                .GroupBy(p => p.Activity)
                .ToDictionary(g => g.Key, g => g.Count()),
            UsersByResourceType = presenceList
                .Where(p => p.ResourceType.HasValue)
                .GroupBy(p => p.ResourceType!.Value)
                .ToDictionary(g => g.Key, g => g.Count()),
            PeakConcurrentUsers = _peakConcurrentUsers,
            PeakTime = _peakTime,
            GeneratedAt = DateTime.UtcNow
        };

        return Task.FromResult(Result<PresenceStats>.Success(stats));
    }

    private string GetColorForUser(Guid userId)
    {
        var index = Math.Abs(userId.GetHashCode()) % _colors.Length;
        return _colors[index];
    }
}
