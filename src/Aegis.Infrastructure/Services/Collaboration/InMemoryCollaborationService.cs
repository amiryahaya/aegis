using System.Collections.Concurrent;
using System.Security.Cryptography;
using Aegis.Domain.Common;
using Aegis.Domain.Services;

namespace Aegis.Infrastructure.Services.Collaboration;

/// <summary>
/// In-memory implementation of collaboration service
/// </summary>
public class InMemoryCollaborationService : ICollaborationService
{
    private readonly ConcurrentDictionary<Guid, Share> _shares = new();
    private readonly ConcurrentDictionary<Guid, ShareableLink> _shareableLinks = new();
    private readonly ConcurrentDictionary<string, Guid> _linkTokenIndex = new();
    private readonly ConcurrentDictionary<Guid, CollaborationWorkspace> _workspaces = new();
    private readonly ConcurrentDictionary<(Guid WorkspaceId, Guid UserId), WorkspaceMember> _workspaceMembers = new();

    public Task<Result<Share>> ShareResourceAsync(
        ShareResourceRequest request,
        CancellationToken cancellationToken = default)
    {
        // Check for existing share with same target
        var existingShare = _shares.Values.FirstOrDefault(s =>
            s.ResourceId == request.ResourceId &&
            s.ResourceType == request.ResourceType &&
            s.Target.Type == request.Target.Type &&
            s.Target.UserId == request.Target.UserId &&
            s.Target.TeamId == request.Target.TeamId &&
            s.Target.WorkspaceId == request.Target.WorkspaceId &&
            s.IsActive);

        if (existingShare != null)
        {
            return Task.FromResult(Result<Share>.Failure(
                Error.Conflict("Share.AlreadyExists", "A share already exists for this target")));
        }

        var share = new Share
        {
            Id = UuidGenerator.NewId(),
            ResourceId = request.ResourceId,
            ResourceType = request.ResourceType,
            SharedBy = request.SharedBy,
            Target = request.Target,
            Permission = request.Permission,
            ExpiresAt = request.ExpiresAt,
            Message = request.Message,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _shares[share.Id] = share;

        return Task.FromResult(Result<Share>.Success(share));
    }

    public Task<Result<Share>> UpdateShareAsync(
        Guid shareId,
        UpdateShareRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_shares.TryGetValue(shareId, out var share))
        {
            return Task.FromResult(Result<Share>.Failure(
                Error.NotFound("Share.NotFound", $"Share with ID {shareId} not found")));
        }

        var updatedShare = share with
        {
            Permission = request.Permission ?? share.Permission,
            ExpiresAt = request.ExpiresAt ?? share.ExpiresAt
        };

        _shares[shareId] = updatedShare;

        return Task.FromResult(Result<Share>.Success(updatedShare));
    }

    public Task<Result> RevokeShareAsync(
        Guid shareId,
        Guid revokedBy,
        CancellationToken cancellationToken = default)
    {
        if (!_shares.TryGetValue(shareId, out var share))
        {
            return Task.FromResult(Result.Failure(
                Error.NotFound("Share.NotFound", $"Share with ID {shareId} not found")));
        }

        var revokedShare = share with { IsActive = false };
        _shares[shareId] = revokedShare;

        return Task.FromResult(Result.Success());
    }

    public Task<Result<Share>> GetShareByIdAsync(
        Guid shareId,
        CancellationToken cancellationToken = default)
    {
        if (!_shares.TryGetValue(shareId, out var share))
        {
            return Task.FromResult(Result<Share>.Failure(
                Error.NotFound("Share.NotFound", $"Share with ID {shareId} not found")));
        }

        return Task.FromResult(Result<Share>.Success(share));
    }

    public Task<Result<IReadOnlyList<Share>>> GetSharesForResourceAsync(
        Guid resourceId,
        ShareableResourceType resourceType,
        CancellationToken cancellationToken = default)
    {
        var shares = _shares.Values
            .Where(s => s.ResourceId == resourceId &&
                        s.ResourceType == resourceType &&
                        s.IsActive &&
                        (s.ExpiresAt == null || s.ExpiresAt > DateTime.UtcNow))
            .OrderByDescending(s => s.CreatedAt)
            .ToList();

        return Task.FromResult(Result<IReadOnlyList<Share>>.Success(shares));
    }

    public Task<Result<SharePage>> GetSharesForUserAsync(
        Guid userId,
        ShareFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        filter ??= new ShareFilter();

        var query = _shares.Values.Where(s =>
            (s.Target.UserId == userId || s.SharedBy == userId) &&
            (filter.IncludeExpired == true || s.IsActive) &&
            (s.ExpiresAt == null || filter.IncludeExpired == true || s.ExpiresAt > DateTime.UtcNow));

        if (filter.ResourceType.HasValue)
            query = query.Where(s => s.ResourceType == filter.ResourceType.Value);

        if (filter.MinPermission.HasValue)
            query = query.Where(s => s.Permission >= filter.MinPermission.Value);

        if (filter.SharedBy.HasValue)
            query = query.Where(s => s.SharedBy == filter.SharedBy.Value);

        var totalCount = query.Count();

        var shares = query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToList();

        var page = new SharePage
        {
            Shares = shares,
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        };

        return Task.FromResult(Result<SharePage>.Success(page));
    }

    public Task<Result<AccessCheckResult>> CheckAccessAsync(
        Guid userId,
        Guid resourceId,
        ShareableResourceType resourceType,
        SharePermission requiredPermission,
        CancellationToken cancellationToken = default)
    {
        // Check direct shares
        var share = _shares.Values.FirstOrDefault(s =>
            s.ResourceId == resourceId &&
            s.ResourceType == resourceType &&
            s.IsActive &&
            (s.ExpiresAt == null || s.ExpiresAt > DateTime.UtcNow) &&
            (s.Target.UserId == userId || s.Target.Type == ShareTargetType.Anyone));

        if (share != null && share.Permission >= requiredPermission)
        {
            return Task.FromResult(Result<AccessCheckResult>.Success(new AccessCheckResult
            {
                HasAccess = true,
                GrantedPermission = share.Permission,
                ShareId = share.Id
            }));
        }

        // Check workspace membership
        var workspaceShare = _shares.Values.FirstOrDefault(s =>
            s.ResourceId == resourceId &&
            s.ResourceType == resourceType &&
            s.IsActive &&
            s.Target.Type == ShareTargetType.Workspace &&
            s.Target.WorkspaceId.HasValue);

        if (workspaceShare != null)
        {
            var isMember = _workspaceMembers.ContainsKey((workspaceShare.Target.WorkspaceId!.Value, userId));
            if (isMember && workspaceShare.Permission >= requiredPermission)
            {
                return Task.FromResult(Result<AccessCheckResult>.Success(new AccessCheckResult
                {
                    HasAccess = true,
                    GrantedPermission = workspaceShare.Permission,
                    ShareId = workspaceShare.Id,
                    Reason = "Access via workspace membership"
                }));
            }
        }

        return Task.FromResult(Result<AccessCheckResult>.Success(new AccessCheckResult
        {
            HasAccess = false,
            Reason = "No matching share found"
        }));
    }

    public Task<Result<ShareableLink>> CreateShareableLinkAsync(
        CreateShareableLinkRequest request,
        CancellationToken cancellationToken = default)
    {
        var token = GenerateSecureToken();

        var link = new ShareableLink
        {
            Id = UuidGenerator.NewId(),
            ResourceId = request.ResourceId,
            ResourceType = request.ResourceType,
            Token = token,
            CreatedBy = request.CreatedBy,
            Permission = request.Permission,
            ExpiresAt = request.ExpiresAt,
            MaxUses = request.MaxUses,
            RequiresAuthentication = request.RequiresAuthentication,
            Password = request.Password != null ? HashPassword(request.Password) : null,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            UseCount = 0
        };

        _shareableLinks[link.Id] = link;
        _linkTokenIndex[token] = link.Id;

        return Task.FromResult(Result<ShareableLink>.Success(link));
    }

    public Task<Result<ShareableLink>> GetShareableLinkAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        if (!_linkTokenIndex.TryGetValue(token, out var linkId))
        {
            return Task.FromResult(Result<ShareableLink>.Failure(
                Error.NotFound("ShareableLink.NotFound", "Link not found")));
        }

        if (!_shareableLinks.TryGetValue(linkId, out var link))
        {
            return Task.FromResult(Result<ShareableLink>.Failure(
                Error.NotFound("ShareableLink.NotFound", "Link not found")));
        }

        if (!link.IsActive)
        {
            return Task.FromResult(Result<ShareableLink>.Failure(
                Error.Validation("ShareableLink.Revoked", "Link has been revoked")));
        }

        if (link.ExpiresAt.HasValue && link.ExpiresAt.Value < DateTime.UtcNow)
        {
            return Task.FromResult(Result<ShareableLink>.Failure(
                Error.Validation("ShareableLink.Expired", "Link has expired")));
        }

        if (link.MaxUses.HasValue && link.UseCount >= link.MaxUses.Value)
        {
            return Task.FromResult(Result<ShareableLink>.Failure(
                Error.Validation("ShareableLink.MaxUsesReached", "Link has reached maximum uses")));
        }

        // Increment use count
        var updatedLink = link with { UseCount = link.UseCount + 1 };
        _shareableLinks[linkId] = updatedLink;

        return Task.FromResult(Result<ShareableLink>.Success(updatedLink));
    }

    public Task<Result> RevokeShareableLinkAsync(
        Guid linkId,
        Guid revokedBy,
        CancellationToken cancellationToken = default)
    {
        if (!_shareableLinks.TryGetValue(linkId, out var link))
        {
            return Task.FromResult(Result.Failure(
                Error.NotFound("ShareableLink.NotFound", $"Link with ID {linkId} not found")));
        }

        var revokedLink = link with { IsActive = false };
        _shareableLinks[linkId] = revokedLink;

        return Task.FromResult(Result.Success());
    }

    public Task<Result<CollaborationWorkspace>> CreateWorkspaceAsync(
        CreateWorkspaceRequest request,
        CancellationToken cancellationToken = default)
    {
        // Check for duplicate name for the owner
        var existingWorkspace = _workspaces.Values.FirstOrDefault(w =>
            w.OwnerId == request.OwnerId &&
            w.Name.Equals(request.Name, StringComparison.OrdinalIgnoreCase) &&
            w.IsActive);

        if (existingWorkspace != null)
        {
            return Task.FromResult(Result<CollaborationWorkspace>.Failure(
                Error.Conflict("Workspace.DuplicateName", "A workspace with this name already exists")));
        }

        var workspace = new CollaborationWorkspace
        {
            Id = UuidGenerator.NewId(),
            Name = request.Name,
            Description = request.Description,
            OwnerId = request.OwnerId,
            Type = request.Type,
            Visibility = request.Visibility,
            Settings = request.Settings ?? new CollaborationWorkspaceSettings(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _workspaces[workspace.Id] = workspace;

        // Add owner as a member
        var ownerMember = new WorkspaceMember
        {
            UserId = request.OwnerId,
            WorkspaceId = workspace.Id,
            DisplayName = "Owner",
            Role = WorkspaceRole.Owner,
            JoinedAt = DateTime.UtcNow,
            Status = MemberStatus.Active
        };

        _workspaceMembers[(workspace.Id, request.OwnerId)] = ownerMember;

        // Return workspace with owner member
        var workspaceWithMembers = workspace with
        {
            Members = new List<WorkspaceMember> { ownerMember }
        };

        return Task.FromResult(Result<CollaborationWorkspace>.Success(workspaceWithMembers));
    }

    public Task<Result<CollaborationWorkspace>> GetWorkspaceByIdAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        if (!_workspaces.TryGetValue(workspaceId, out var workspace))
        {
            return Task.FromResult(Result<CollaborationWorkspace>.Failure(
                Error.NotFound("Workspace.NotFound", $"Workspace with ID {workspaceId} not found")));
        }

        var members = _workspaceMembers.Values
            .Where(m => m.WorkspaceId == workspaceId)
            .ToList();

        var workspaceWithMembers = workspace with { Members = members };

        return Task.FromResult(Result<CollaborationWorkspace>.Success(workspaceWithMembers));
    }

    public Task<Result<IReadOnlyList<CollaborationWorkspace>>> GetWorkspacesForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var workspaceIds = _workspaceMembers.Values
            .Where(m => m.UserId == userId && m.Status == MemberStatus.Active)
            .Select(m => m.WorkspaceId)
            .ToHashSet();

        var workspaces = _workspaces.Values
            .Where(w => workspaceIds.Contains(w.Id) && w.IsActive)
            .Select(w =>
            {
                var members = _workspaceMembers.Values
                    .Where(m => m.WorkspaceId == w.Id)
                    .ToList();
                return w with { Members = members };
            })
            .OrderByDescending(w => w.UpdatedAt)
            .ToList();

        return Task.FromResult(Result<IReadOnlyList<CollaborationWorkspace>>.Success(workspaces));
    }

    public Task<Result<WorkspaceMember>> AddWorkspaceMemberAsync(
        Guid workspaceId,
        AddWorkspaceMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_workspaces.TryGetValue(workspaceId, out var workspace))
        {
            return Task.FromResult(Result<WorkspaceMember>.Failure(
                Error.NotFound("Workspace.NotFound", $"Workspace with ID {workspaceId} not found")));
        }

        var key = (workspaceId, request.UserId);
        if (_workspaceMembers.ContainsKey(key))
        {
            return Task.FromResult(Result<WorkspaceMember>.Failure(
                Error.Conflict("Workspace.MemberExists", "User is already a member of this workspace")));
        }

        // Check max members
        var currentMemberCount = _workspaceMembers.Values.Count(m => m.WorkspaceId == workspaceId);
        if (currentMemberCount >= workspace.Settings.MaxMembers)
        {
            return Task.FromResult(Result<WorkspaceMember>.Failure(
                Error.Validation("Workspace.MaxMembers", "Workspace has reached maximum member limit")));
        }

        var member = new WorkspaceMember
        {
            UserId = request.UserId,
            WorkspaceId = workspaceId,
            DisplayName = request.DisplayName,
            Email = request.Email,
            Role = request.Role,
            JoinedAt = DateTime.UtcNow,
            Status = workspace.Settings.RequireApprovalForJoin ? MemberStatus.Pending : MemberStatus.Active
        };

        _workspaceMembers[key] = member;

        return Task.FromResult(Result<WorkspaceMember>.Success(member));
    }

    public Task<Result> RemoveWorkspaceMemberAsync(
        Guid workspaceId,
        Guid userId,
        Guid removedBy,
        CancellationToken cancellationToken = default)
    {
        var key = (workspaceId, userId);
        if (!_workspaceMembers.TryGetValue(key, out var member))
        {
            return Task.FromResult(Result.Failure(
                Error.NotFound("Workspace.MemberNotFound", "Member not found in workspace")));
        }

        // Cannot remove owner
        if (member.Role == WorkspaceRole.Owner)
        {
            return Task.FromResult(Result.Failure(
                Error.Validation("Workspace.CannotRemoveOwner", "Cannot remove workspace owner")));
        }

        _workspaceMembers.TryRemove(key, out _);

        return Task.FromResult(Result.Success());
    }

    public Task<Result<WorkspaceMember>> UpdateMemberRoleAsync(
        Guid workspaceId,
        Guid userId,
        WorkspaceRole newRole,
        Guid updatedBy,
        CancellationToken cancellationToken = default)
    {
        var key = (workspaceId, userId);
        if (!_workspaceMembers.TryGetValue(key, out var member))
        {
            return Task.FromResult(Result<WorkspaceMember>.Failure(
                Error.NotFound("Workspace.MemberNotFound", "Member not found in workspace")));
        }

        // Cannot change owner role
        if (member.Role == WorkspaceRole.Owner && newRole != WorkspaceRole.Owner)
        {
            return Task.FromResult(Result<WorkspaceMember>.Failure(
                Error.Validation("Workspace.CannotChangeOwnerRole", "Cannot change owner role. Use TransferOwnership instead.")));
        }

        // Cannot promote to owner
        if (newRole == WorkspaceRole.Owner)
        {
            return Task.FromResult(Result<WorkspaceMember>.Failure(
                Error.Validation("Workspace.CannotPromoteToOwner", "Cannot promote to owner. Use TransferOwnership instead.")));
        }

        var updatedMember = member with { Role = newRole };
        _workspaceMembers[key] = updatedMember;

        return Task.FromResult(Result<WorkspaceMember>.Success(updatedMember));
    }

    public Task<Result<IReadOnlyList<WorkspaceMember>>> GetWorkspaceMembersAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        if (!_workspaces.ContainsKey(workspaceId))
        {
            return Task.FromResult(Result<IReadOnlyList<WorkspaceMember>>.Failure(
                Error.NotFound("Workspace.NotFound", $"Workspace with ID {workspaceId} not found")));
        }

        var members = _workspaceMembers.Values
            .Where(m => m.WorkspaceId == workspaceId)
            .OrderByDescending(m => m.Role)
            .ThenBy(m => m.DisplayName)
            .ToList();

        return Task.FromResult(Result<IReadOnlyList<WorkspaceMember>>.Success(members));
    }

    public Task<Result> TransferOwnershipAsync(
        Guid workspaceId,
        Guid newOwnerId,
        Guid currentOwnerId,
        CancellationToken cancellationToken = default)
    {
        if (!_workspaces.TryGetValue(workspaceId, out var workspace))
        {
            return Task.FromResult(Result.Failure(
                Error.NotFound("Workspace.NotFound", $"Workspace with ID {workspaceId} not found")));
        }

        if (workspace.OwnerId != currentOwnerId)
        {
            return Task.FromResult(Result.Failure(
                Error.Validation("Workspace.NotOwner", "Only the owner can transfer ownership")));
        }

        var newOwnerKey = (workspaceId, newOwnerId);
        if (!_workspaceMembers.TryGetValue(newOwnerKey, out var newOwnerMember))
        {
            return Task.FromResult(Result.Failure(
                Error.NotFound("Workspace.MemberNotFound", "New owner must be a member of the workspace")));
        }

        // Update workspace owner
        var updatedWorkspace = workspace with { OwnerId = newOwnerId, UpdatedAt = DateTime.UtcNow };
        _workspaces[workspaceId] = updatedWorkspace;

        // Update member roles
        var currentOwnerKey = (workspaceId, currentOwnerId);
        if (_workspaceMembers.TryGetValue(currentOwnerKey, out var currentOwnerMember))
        {
            _workspaceMembers[currentOwnerKey] = currentOwnerMember with { Role = WorkspaceRole.Admin };
        }
        _workspaceMembers[newOwnerKey] = newOwnerMember with { Role = WorkspaceRole.Owner };

        return Task.FromResult(Result.Success());
    }

    public Task<Result> DeleteWorkspaceAsync(
        Guid workspaceId,
        Guid deletedBy,
        CancellationToken cancellationToken = default)
    {
        if (!_workspaces.TryGetValue(workspaceId, out var workspace))
        {
            return Task.FromResult(Result.Failure(
                Error.NotFound("Workspace.NotFound", $"Workspace with ID {workspaceId} not found")));
        }

        if (workspace.OwnerId != deletedBy)
        {
            return Task.FromResult(Result.Failure(
                Error.Validation("Workspace.NotOwner", "Only the owner can delete the workspace")));
        }

        // Soft delete
        var deletedWorkspace = workspace with { IsActive = false, UpdatedAt = DateTime.UtcNow };
        _workspaces[workspaceId] = deletedWorkspace;

        // Remove all members
        var memberKeys = _workspaceMembers.Keys.Where(k => k.WorkspaceId == workspaceId).ToList();
        foreach (var key in memberKeys)
        {
            _workspaceMembers.TryRemove(key, out _);
        }

        // Revoke all shares for this workspace
        var workspaceShares = _shares.Values
            .Where(s => s.Target.WorkspaceId == workspaceId)
            .ToList();
        foreach (var share in workspaceShares)
        {
            _shares[share.Id] = share with { IsActive = false };
        }

        return Task.FromResult(Result.Success());
    }

    public Task<Result<CollaborationStats>> GetStatsAsync(
        Guid? workspaceId = null,
        CancellationToken cancellationToken = default)
    {
        var sharesQuery = _shares.Values.AsEnumerable();
        var linksQuery = _shareableLinks.Values.AsEnumerable();
        var workspacesQuery = _workspaces.Values.AsEnumerable();
        var membersQuery = _workspaceMembers.Values.AsEnumerable();

        if (workspaceId.HasValue)
        {
            sharesQuery = sharesQuery.Where(s => s.Target.WorkspaceId == workspaceId);
            workspacesQuery = workspacesQuery.Where(w => w.Id == workspaceId);
            membersQuery = membersQuery.Where(m => m.WorkspaceId == workspaceId);
        }

        var shares = sharesQuery.ToList();
        var links = linksQuery.ToList();
        var workspaces = workspacesQuery.ToList();
        var members = membersQuery.ToList();

        var stats = new CollaborationStats
        {
            TotalWorkspaces = workspaces.Count,
            ActiveWorkspaces = workspaces.Count(w => w.IsActive),
            TotalShares = shares.Count,
            ActiveShares = shares.Count(s => s.IsActive && (s.ExpiresAt == null || s.ExpiresAt > DateTime.UtcNow)),
            TotalShareableLinks = links.Count,
            ActiveShareableLinks = links.Count(l => l.IsActive && (l.ExpiresAt == null || l.ExpiresAt > DateTime.UtcNow)),
            TotalMembers = members.Count,
            SharesByResourceType = shares
                .GroupBy(s => s.ResourceType)
                .ToDictionary(g => g.Key, g => g.Count()),
            SharesByPermission = shares
                .GroupBy(s => s.Permission)
                .ToDictionary(g => g.Key, g => g.Count()),
            WorkspacesByType = workspaces
                .GroupBy(w => w.Type)
                .ToDictionary(g => g.Key, g => g.Count()),
            GeneratedAt = DateTime.UtcNow
        };

        return Task.FromResult(Result<CollaborationStats>.Success(stats));
    }

    private static string GenerateSecureToken()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    private static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(password);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}
