using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.Collaboration;
using FluentAssertions;
using Xunit;

namespace Aegis.UnitTests.Services.Collaboration;

public class CollaborationServiceTests
{
    private readonly InMemoryCollaborationService _service;

    public CollaborationServiceTests()
    {
        _service = new InMemoryCollaborationService();
    }

    #region Workspace Tests

    [Fact]
    public async Task CreateWorkspaceAsync_WithValidRequest_ShouldSucceed()
    {
        // Arrange
        var request = new CreateWorkspaceRequest
        {
            Name = "Test Workspace",
            Description = "Test Description",
            OwnerId = Guid.NewGuid(),
            Type = CollaborationWorkspaceType.Team,
            Visibility = CollaborationWorkspaceVisibility.Private
        };

        // Act
        var result = await _service.CreateWorkspaceAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Test Workspace");
        result.Value.OwnerId.Should().Be(request.OwnerId);
        result.Value.Members.Should().HaveCount(1);
        result.Value.Members[0].Role.Should().Be(WorkspaceRole.Owner);
    }

    [Fact]
    public async Task CreateWorkspaceAsync_WithDuplicateName_ShouldFail()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var request = new CreateWorkspaceRequest
        {
            Name = "Duplicate Workspace",
            OwnerId = ownerId
        };

        await _service.CreateWorkspaceAsync(request);

        // Act
        var result = await _service.CreateWorkspaceAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Contain("DuplicateName");
    }

    [Fact]
    public async Task GetWorkspaceByIdAsync_WithExistingWorkspace_ShouldSucceed()
    {
        // Arrange
        var request = new CreateWorkspaceRequest
        {
            Name = "Get Test Workspace",
            OwnerId = Guid.NewGuid()
        };
        var created = await _service.CreateWorkspaceAsync(request);

        // Act
        var result = await _service.GetWorkspaceByIdAsync(created.Value.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Get Test Workspace");
    }

    [Fact]
    public async Task GetWorkspaceByIdAsync_WithNonExistentId_ShouldFail()
    {
        // Act
        var result = await _service.GetWorkspaceByIdAsync(Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Contain("NotFound");
    }

    [Fact]
    public async Task AddWorkspaceMemberAsync_WithValidRequest_ShouldSucceed()
    {
        // Arrange
        var workspace = await _service.CreateWorkspaceAsync(new CreateWorkspaceRequest
        {
            Name = "Member Test Workspace",
            OwnerId = Guid.NewGuid()
        });

        var request = new AddWorkspaceMemberRequest
        {
            UserId = Guid.NewGuid(),
            DisplayName = "New Member",
            Role = WorkspaceRole.Member,
            AddedBy = workspace.Value.OwnerId
        };

        // Act
        var result = await _service.AddWorkspaceMemberAsync(workspace.Value.Id, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.DisplayName.Should().Be("New Member");
        result.Value.Role.Should().Be(WorkspaceRole.Member);
    }

    [Fact]
    public async Task RemoveWorkspaceMemberAsync_ShouldRemoveMember()
    {
        // Arrange
        var workspace = await _service.CreateWorkspaceAsync(new CreateWorkspaceRequest
        {
            Name = "Remove Test Workspace",
            OwnerId = Guid.NewGuid()
        });

        var memberId = Guid.NewGuid();
        await _service.AddWorkspaceMemberAsync(workspace.Value.Id, new AddWorkspaceMemberRequest
        {
            UserId = memberId,
            DisplayName = "Member to Remove",
            AddedBy = workspace.Value.OwnerId
        });

        // Act
        var result = await _service.RemoveWorkspaceMemberAsync(
            workspace.Value.Id, memberId, workspace.Value.OwnerId);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task TransferOwnershipAsync_ShouldTransferOwnership()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var workspace = await _service.CreateWorkspaceAsync(new CreateWorkspaceRequest
        {
            Name = "Transfer Test Workspace",
            OwnerId = ownerId
        });

        var newOwnerId = Guid.NewGuid();
        await _service.AddWorkspaceMemberAsync(workspace.Value.Id, new AddWorkspaceMemberRequest
        {
            UserId = newOwnerId,
            DisplayName = "New Owner",
            AddedBy = ownerId
        });

        // Act
        var result = await _service.TransferOwnershipAsync(
            workspace.Value.Id, newOwnerId, ownerId);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var updated = await _service.GetWorkspaceByIdAsync(workspace.Value.Id);
        updated.Value.OwnerId.Should().Be(newOwnerId);
    }

    #endregion

    #region Share Tests

    [Fact]
    public async Task ShareResourceAsync_WithValidRequest_ShouldSucceed()
    {
        // Arrange
        var request = new ShareResourceRequest
        {
            ResourceId = Guid.NewGuid(),
            ResourceType = ShareableResourceType.Session,
            SharedBy = Guid.NewGuid(),
            Target = new ShareTarget
            {
                Type = ShareTargetType.User,
                UserId = Guid.NewGuid()
            },
            Permission = SharePermission.View
        };

        // Act
        var result = await _service.ShareResourceAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Permission.Should().Be(SharePermission.View);
        result.Value.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ShareResourceAsync_WithDuplicateShare_ShouldFail()
    {
        // Arrange
        var targetUserId = Guid.NewGuid();
        var request = new ShareResourceRequest
        {
            ResourceId = Guid.NewGuid(),
            ResourceType = ShareableResourceType.Document,
            SharedBy = Guid.NewGuid(),
            Target = new ShareTarget
            {
                Type = ShareTargetType.User,
                UserId = targetUserId
            }
        };

        await _service.ShareResourceAsync(request);

        // Act
        var result = await _service.ShareResourceAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Contain("AlreadyExists");
    }

    [Fact]
    public async Task CheckAccessAsync_WithValidShare_ShouldGrantAccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();

        await _service.ShareResourceAsync(new ShareResourceRequest
        {
            ResourceId = resourceId,
            ResourceType = ShareableResourceType.Session,
            SharedBy = Guid.NewGuid(),
            Target = new ShareTarget
            {
                Type = ShareTargetType.User,
                UserId = userId
            },
            Permission = SharePermission.Edit
        });

        // Act
        var result = await _service.CheckAccessAsync(
            userId, resourceId, ShareableResourceType.Session, SharePermission.View);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.HasAccess.Should().BeTrue();
        result.Value.GrantedPermission.Should().Be(SharePermission.Edit);
    }

    [Fact]
    public async Task RevokeShareAsync_ShouldDeactivateShare()
    {
        // Arrange
        var share = await _service.ShareResourceAsync(new ShareResourceRequest
        {
            ResourceId = Guid.NewGuid(),
            ResourceType = ShareableResourceType.Document,
            SharedBy = Guid.NewGuid(),
            Target = new ShareTarget
            {
                Type = ShareTargetType.User,
                UserId = Guid.NewGuid()
            }
        });

        // Act
        var result = await _service.RevokeShareAsync(share.Value.Id, Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeTrue();

        var revoked = await _service.GetShareByIdAsync(share.Value.Id);
        revoked.Value.IsActive.Should().BeFalse();
    }

    #endregion

    #region Shareable Link Tests

    [Fact]
    public async Task CreateShareableLinkAsync_ShouldCreateLink()
    {
        // Arrange
        var request = new CreateShareableLinkRequest
        {
            ResourceId = Guid.NewGuid(),
            ResourceType = ShareableResourceType.Report,
            CreatedBy = Guid.NewGuid(),
            Permission = SharePermission.View,
            MaxUses = 10
        };

        // Act
        var result = await _service.CreateShareableLinkAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Token.Should().NotBeNullOrEmpty();
        result.Value.MaxUses.Should().Be(10);
    }

    [Fact]
    public async Task GetShareableLinkAsync_ShouldIncrementUseCount()
    {
        // Arrange
        var link = await _service.CreateShareableLinkAsync(new CreateShareableLinkRequest
        {
            ResourceId = Guid.NewGuid(),
            ResourceType = ShareableResourceType.Report,
            CreatedBy = Guid.NewGuid()
        });

        // Act
        var result1 = await _service.GetShareableLinkAsync(link.Value.Token);
        var result2 = await _service.GetShareableLinkAsync(link.Value.Token);

        // Assert
        result1.Value.UseCount.Should().Be(1);
        result2.Value.UseCount.Should().Be(2);
    }

    [Fact]
    public async Task GetShareableLinkAsync_WithMaxUsesReached_ShouldFail()
    {
        // Arrange
        var link = await _service.CreateShareableLinkAsync(new CreateShareableLinkRequest
        {
            ResourceId = Guid.NewGuid(),
            ResourceType = ShareableResourceType.Report,
            CreatedBy = Guid.NewGuid(),
            MaxUses = 1
        });

        await _service.GetShareableLinkAsync(link.Value.Token);

        // Act
        var result = await _service.GetShareableLinkAsync(link.Value.Token);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Contain("MaxUsesReached");
    }

    #endregion

    #region Statistics Tests

    [Fact]
    public async Task GetStatsAsync_ShouldReturnStatistics()
    {
        // Arrange
        await _service.CreateWorkspaceAsync(new CreateWorkspaceRequest
        {
            Name = "Stats Workspace",
            OwnerId = Guid.NewGuid()
        });

        await _service.ShareResourceAsync(new ShareResourceRequest
        {
            ResourceId = Guid.NewGuid(),
            ResourceType = ShareableResourceType.Session,
            SharedBy = Guid.NewGuid(),
            Target = new ShareTarget
            {
                Type = ShareTargetType.User,
                UserId = Guid.NewGuid()
            }
        });

        // Act
        var result = await _service.GetStatsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalWorkspaces.Should().BeGreaterThanOrEqualTo(1);
        result.Value.TotalShares.Should().BeGreaterThanOrEqualTo(1);
    }

    #endregion
}

public class PresenceServiceTests
{
    private readonly InMemoryPresenceService _service;

    public PresenceServiceTests()
    {
        _service = new InMemoryPresenceService();
    }

    [Fact]
    public async Task JoinResourceAsync_ShouldCreatePresence()
    {
        // Arrange
        var request = new JoinResourceRequest
        {
            UserId = Guid.NewGuid(),
            DisplayName = "Test User",
            ResourceId = Guid.NewGuid(),
            ResourceType = PresenceResourceType.Session,
            ConnectionId = "conn1"
        };

        // Act
        var result = await _service.JoinResourceAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.DisplayName.Should().Be("Test User");
        result.Value.Status.Should().Be(PresenceStatus.Online);
        result.Value.Color.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetResourcePresenceAsync_ShouldReturnActiveUsers()
    {
        // Arrange
        var resourceId = Guid.NewGuid();

        await _service.JoinResourceAsync(new JoinResourceRequest
        {
            UserId = Guid.NewGuid(),
            DisplayName = "User 1",
            ResourceId = resourceId,
            ResourceType = PresenceResourceType.Session
        });

        await _service.JoinResourceAsync(new JoinResourceRequest
        {
            UserId = Guid.NewGuid(),
            DisplayName = "User 2",
            ResourceId = resourceId,
            ResourceType = PresenceResourceType.Session
        });

        // Act
        var result = await _service.GetResourcePresenceAsync(resourceId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task LeaveResourceAsync_ShouldRemovePresence()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();

        await _service.JoinResourceAsync(new JoinResourceRequest
        {
            UserId = userId,
            DisplayName = "Leaving User",
            ResourceId = resourceId,
            ResourceType = PresenceResourceType.Session
        });

        // Act
        await _service.LeaveResourceAsync(userId, resourceId);
        var result = await _service.GetResourcePresenceAsync(resourceId);

        // Assert
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateCursorPositionAsync_ShouldUpdatePosition()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();

        await _service.JoinResourceAsync(new JoinResourceRequest
        {
            UserId = userId,
            DisplayName = "Cursor User",
            ResourceId = resourceId,
            ResourceType = PresenceResourceType.Document
        });

        var position = new CursorPosition { Line = 10, Column = 5 };

        // Act
        var result = await _service.UpdateCursorPositionAsync(userId, resourceId, position);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var cursors = await _service.GetCursorPositionsAsync(resourceId);
        cursors.Value.Should().HaveCount(1);
        cursors.Value[0].Position.Line.Should().Be(10);
    }

    [Fact]
    public async Task SetStatusAsync_ShouldUpdateStatus()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await _service.JoinResourceAsync(new JoinResourceRequest
        {
            UserId = userId,
            DisplayName = "Status User",
            ResourceId = Guid.NewGuid(),
            ResourceType = PresenceResourceType.Session
        });

        // Act
        var result = await _service.SetStatusAsync(userId, PresenceStatus.Away, "In a meeting");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(PresenceStatus.Away);
        result.Value.StatusMessage.Should().Be("In a meeting");
    }

    [Fact]
    public async Task CleanupStalePresenceAsync_ShouldRemoveStaleRecords()
    {
        // Arrange - presence is created but not updated, so it will be stale
        await _service.JoinResourceAsync(new JoinResourceRequest
        {
            UserId = Guid.NewGuid(),
            DisplayName = "Stale User",
            ResourceId = Guid.NewGuid(),
            ResourceType = PresenceResourceType.Session
        });

        // Act
        var result = await _service.CleanupStalePresenceAsync(TimeSpan.Zero);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeGreaterThanOrEqualTo(1);
    }
}

public class CommentServiceTests
{
    private readonly InMemoryCommentService _service;

    public CommentServiceTests()
    {
        _service = new InMemoryCommentService();
    }

    [Fact]
    public async Task CreateCommentAsync_ShouldCreateComment()
    {
        // Arrange
        var request = new CreateCommentRequest
        {
            ResourceId = Guid.NewGuid(),
            ResourceType = CommentableResourceType.Session,
            AuthorId = Guid.NewGuid(),
            AuthorName = "Test Author",
            Content = "This is a test comment"
        };

        // Act
        var result = await _service.CreateCommentAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Content.Should().Be("This is a test comment");
        result.Value.AuthorName.Should().Be("Test Author");
    }

    [Fact]
    public async Task CreateCommentAsync_WithParent_ShouldCreateReply()
    {
        // Arrange
        var parent = await _service.CreateCommentAsync(new CreateCommentRequest
        {
            ResourceId = Guid.NewGuid(),
            ResourceType = CommentableResourceType.Document,
            AuthorId = Guid.NewGuid(),
            AuthorName = "Parent Author",
            Content = "Parent comment"
        });

        // Act
        var reply = await _service.CreateCommentAsync(new CreateCommentRequest
        {
            ResourceId = parent.Value.ResourceId,
            ResourceType = CommentableResourceType.Document,
            AuthorId = Guid.NewGuid(),
            AuthorName = "Reply Author",
            Content = "Reply comment",
            ParentCommentId = parent.Value.Id
        });

        // Assert
        reply.IsSuccess.Should().BeTrue();
        reply.Value.ParentCommentId.Should().Be(parent.Value.Id);
        reply.Value.ThreadRootId.Should().Be(parent.Value.Id);
    }

    [Fact]
    public async Task UpdateCommentAsync_ByAuthor_ShouldSucceed()
    {
        // Arrange
        var authorId = Guid.NewGuid();
        var comment = await _service.CreateCommentAsync(new CreateCommentRequest
        {
            ResourceId = Guid.NewGuid(),
            ResourceType = CommentableResourceType.Query,
            AuthorId = authorId,
            AuthorName = "Author",
            Content = "Original content"
        });

        // Act
        var result = await _service.UpdateCommentAsync(comment.Value.Id, new UpdateCommentRequest
        {
            Content = "Updated content",
            UpdatedBy = authorId
        });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Content.Should().Be("Updated content");
        result.Value.IsEdited.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateCommentAsync_ByNonAuthor_ShouldFail()
    {
        // Arrange
        var comment = await _service.CreateCommentAsync(new CreateCommentRequest
        {
            ResourceId = Guid.NewGuid(),
            ResourceType = CommentableResourceType.Query,
            AuthorId = Guid.NewGuid(),
            AuthorName = "Author",
            Content = "Content"
        });

        // Act
        var result = await _service.UpdateCommentAsync(comment.Value.Id, new UpdateCommentRequest
        {
            Content = "Hacked content",
            UpdatedBy = Guid.NewGuid() // Different user
        });

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Contain("NotAuthor");
    }

    [Fact]
    public async Task AddReactionAsync_ShouldAddReaction()
    {
        // Arrange
        var comment = await _service.CreateCommentAsync(new CreateCommentRequest
        {
            ResourceId = Guid.NewGuid(),
            ResourceType = CommentableResourceType.Document,
            AuthorId = Guid.NewGuid(),
            AuthorName = "Author",
            Content = "React to me"
        });

        // Act
        var result = await _service.AddReactionAsync(comment.Value.Id, new AddReactionRequest
        {
            UserId = Guid.NewGuid(),
            UserName = "Reactor",
            Type = ReactionType.Like
        });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Type.Should().Be(ReactionType.Like);
    }

    [Fact]
    public async Task AddReactionAsync_Duplicate_ShouldFail()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var comment = await _service.CreateCommentAsync(new CreateCommentRequest
        {
            ResourceId = Guid.NewGuid(),
            ResourceType = CommentableResourceType.Document,
            AuthorId = Guid.NewGuid(),
            AuthorName = "Author",
            Content = "Content"
        });

        await _service.AddReactionAsync(comment.Value.Id, new AddReactionRequest
        {
            UserId = userId,
            UserName = "User",
            Type = ReactionType.Like
        });

        // Act
        var result = await _service.AddReactionAsync(comment.Value.Id, new AddReactionRequest
        {
            UserId = userId,
            UserName = "User",
            Type = ReactionType.Like
        });

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Contain("AlreadyExists");
    }

    [Fact]
    public async Task ResolveCommentAsync_ShouldMarkAsResolved()
    {
        // Arrange
        var comment = await _service.CreateCommentAsync(new CreateCommentRequest
        {
            ResourceId = Guid.NewGuid(),
            ResourceType = CommentableResourceType.Session,
            AuthorId = Guid.NewGuid(),
            AuthorName = "Author",
            Content = "Resolvable comment"
        });

        var resolverId = Guid.NewGuid();

        // Act
        var result = await _service.ResolveCommentAsync(comment.Value.Id, resolverId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsResolved.Should().BeTrue();
        result.Value.ResolvedBy.Should().Be(resolverId);
    }

    [Fact]
    public async Task PinCommentAsync_ShouldPinComment()
    {
        // Arrange
        var comment = await _service.CreateCommentAsync(new CreateCommentRequest
        {
            ResourceId = Guid.NewGuid(),
            ResourceType = CommentableResourceType.Document,
            AuthorId = Guid.NewGuid(),
            AuthorName = "Author",
            Content = "Pin me"
        });

        // Act
        var result = await _service.PinCommentAsync(comment.Value.Id, Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsPinned.Should().BeTrue();
    }
}

public class ActivityFeedServiceTests
{
    private readonly InMemoryActivityFeedService _service;

    public ActivityFeedServiceTests()
    {
        _service = new InMemoryActivityFeedService();
    }

    [Fact]
    public async Task RecordActivityAsync_ShouldCreateActivity()
    {
        // Arrange
        var request = new RecordActivityRequest
        {
            ActorId = Guid.NewGuid(),
            ActorName = "Test User",
            Type = FeedActivityType.Resource,
            Verb = ActivityVerb.Created,
            ResourceId = Guid.NewGuid(),
            ResourceType = ActivityResourceType.Document,
            ResourceName = "Test Document",
            WorkspaceId = Guid.NewGuid()
        };

        // Act
        var result = await _service.RecordActivityAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ActorName.Should().Be("Test User");
        result.Value.Verb.Should().Be(ActivityVerb.Created);
    }

    [Fact]
    public async Task GetWorkspaceActivitiesAsync_ShouldReturnActivities()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();

        await _service.RecordActivityAsync(new RecordActivityRequest
        {
            ActorId = Guid.NewGuid(),
            ActorName = "User 1",
            Type = FeedActivityType.Resource,
            Verb = ActivityVerb.Created,
            WorkspaceId = workspaceId
        });

        await _service.RecordActivityAsync(new RecordActivityRequest
        {
            ActorId = Guid.NewGuid(),
            ActorName = "User 2",
            Type = FeedActivityType.Comment,
            Verb = ActivityVerb.Commented,
            WorkspaceId = workspaceId
        });

        // Act
        var result = await _service.GetWorkspaceActivitiesAsync(workspaceId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task MarkAsSeenAsync_ShouldMarkActivityAsSeen()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();

        var activity = await _service.RecordActivityAsync(new RecordActivityRequest
        {
            ActorId = Guid.NewGuid(),
            ActorName = "Other User",
            Type = FeedActivityType.Resource,
            Verb = ActivityVerb.Updated,
            WorkspaceId = workspaceId,
            RelatedUserIds = new List<Guid> { userId }
        });

        // Act
        await _service.MarkAsSeenAsync(userId, new[] { activity.Value.Id });
        var unseenCount = await _service.GetUnseenCountAsync(userId, workspaceId);

        // Assert
        unseenCount.Value.Should().Be(0);
    }

    [Fact]
    public async Task SubscribeAsync_ShouldCreateSubscription()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new SubscribeToActivityRequest
        {
            UserId = userId,
            ResourceId = Guid.NewGuid(),
            ResourceType = ActivityResourceType.Session,
            Scope = SubscriptionScope.Resource
        };

        // Act
        var result = await _service.SubscribeAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(userId);
    }

    [Fact]
    public async Task UnsubscribeAsync_ShouldRemoveSubscription()
    {
        // Arrange
        var subscription = await _service.SubscribeAsync(new SubscribeToActivityRequest
        {
            UserId = Guid.NewGuid(),
            WorkspaceId = Guid.NewGuid(),
            Scope = SubscriptionScope.Workspace
        });

        // Act
        var result = await _service.UnsubscribeAsync(subscription.Value.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GetStatsAsync_ShouldReturnStatistics()
    {
        // Arrange
        await _service.RecordActivityAsync(new RecordActivityRequest
        {
            ActorId = Guid.NewGuid(),
            ActorName = "User",
            Type = FeedActivityType.Resource,
            Verb = ActivityVerb.Created
        });

        // Act
        var result = await _service.GetStatsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalActivities.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task PurgeOldActivitiesAsync_ShouldRemoveOldActivities()
    {
        // Arrange
        await _service.RecordActivityAsync(new RecordActivityRequest
        {
            ActorId = Guid.NewGuid(),
            ActorName = "Old User",
            Type = FeedActivityType.System,
            Verb = ActivityVerb.Updated
        });

        // Act - purge with zero retention (should delete all)
        var result = await _service.PurgeOldActivitiesAsync(TimeSpan.Zero);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeGreaterThanOrEqualTo(1);
    }
}
