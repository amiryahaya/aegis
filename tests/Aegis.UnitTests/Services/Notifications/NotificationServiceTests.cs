using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.Notifications;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Aegis.UnitTests.Services.Notifications;

public class NotificationServiceTests
{
    private readonly ILogger<InMemoryNotificationService> _logger;
    private readonly InMemoryNotificationService _sut;

    public NotificationServiceTests()
    {
        _logger = Substitute.For<ILogger<InMemoryNotificationService>>();
        _sut = new InMemoryNotificationService(_logger);
    }

    #region SendAsync Tests

    [Fact]
    public async Task SendAsync_WithValidRequest_ShouldCreateNotification()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new SendNotificationRequest
        {
            UserId = userId,
            Type = NotificationType.DocumentProcessed,
            Title = "Document Processed",
            Message = "Your document has been processed successfully",
            Priority = NotificationPriority.Normal
        };

        // Act
        var result = await _sut.SendAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(userId);
        result.Value.Type.Should().Be(NotificationType.DocumentProcessed);
        result.Value.Title.Should().Be("Document Processed");
        result.Value.Status.Should().Be(NotificationStatus.Unread);
    }

    [Fact]
    public async Task SendAsync_WithHighPriority_ShouldPreservePriority()
    {
        // Arrange
        var request = new SendNotificationRequest
        {
            UserId = Guid.NewGuid(),
            Type = NotificationType.SuspiciousActivity,
            Title = "Security Alert",
            Message = "Suspicious activity detected",
            Priority = NotificationPriority.Urgent
        };

        // Act
        var result = await _sut.SendAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Priority.Should().Be(NotificationPriority.Urgent);
    }

    [Fact]
    public async Task SendAsync_WithActionUrl_ShouldPreserveActionDetails()
    {
        // Arrange
        var request = new SendNotificationRequest
        {
            UserId = Guid.NewGuid(),
            Type = NotificationType.QueryCompleted,
            Title = "Query Complete",
            Message = "Your query has finished",
            ActionUrl = "/queries/123",
            ActionLabel = "View Results"
        };

        // Act
        var result = await _sut.SendAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ActionUrl.Should().Be("/queries/123");
        result.Value.ActionLabel.Should().Be("View Results");
    }

    [Fact]
    public async Task SendAsync_WithCustomData_ShouldPreserveData()
    {
        // Arrange
        var request = new SendNotificationRequest
        {
            UserId = Guid.NewGuid(),
            Type = NotificationType.Custom,
            Title = "Custom Notification",
            Message = "Test message",
            Data = new Dictionary<string, object>
            {
                ["key1"] = "value1",
                ["key2"] = 42
            }
        };

        // Act
        var result = await _sut.SendAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Data.Should().ContainKey("key1");
        result.Value.Data["key1"].Should().Be("value1");
    }

    [Fact]
    public async Task SendAsync_WithTeamId_ShouldAssociateWithTeam()
    {
        // Arrange
        var teamId = Guid.NewGuid();
        var request = new SendNotificationRequest
        {
            UserId = Guid.NewGuid(),
            Type = NotificationType.TeamInvitation,
            Title = "Team Invitation",
            Message = "You've been invited to join a team",
            TeamId = teamId
        };

        // Act
        var result = await _sut.SendAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TeamId.Should().Be(teamId);
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_WithExistingId_ShouldReturnNotification()
    {
        // Arrange
        var request = new SendNotificationRequest
        {
            UserId = Guid.NewGuid(),
            Type = NotificationType.SystemAlert,
            Title = "Alert",
            Message = "System alert"
        };
        var sendResult = await _sut.SendAsync(request);
        var notificationId = sendResult.Value.Id;

        // Act
        var result = await _sut.GetByIdAsync(notificationId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(notificationId);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistingId_ShouldReturnNotFound()
    {
        // Act
        var result = await _sut.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Contain("NotFound");
    }

    #endregion

    #region GetForUserAsync Tests

    [Fact]
    public async Task GetForUserAsync_WithNoNotifications_ShouldReturnEmptyPage()
    {
        // Act
        var result = await _sut.GetForUserAsync(Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetForUserAsync_WithMultipleNotifications_ShouldReturnAll()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await _sut.SendAsync(CreateRequest(userId, "Notification 1"));
        await _sut.SendAsync(CreateRequest(userId, "Notification 2"));
        await _sut.SendAsync(CreateRequest(userId, "Notification 3"));

        // Act
        var result = await _sut.GetForUserAsync(userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(3);
        result.Value.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task GetForUserAsync_WithTypeFilter_ShouldFilterByType()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await _sut.SendAsync(CreateRequest(userId, "Query", NotificationType.QueryCompleted));
        await _sut.SendAsync(CreateRequest(userId, "Document", NotificationType.DocumentProcessed));
        await _sut.SendAsync(CreateRequest(userId, "Alert", NotificationType.SystemAlert));

        var filter = new NotificationFilter { Type = NotificationType.QueryCompleted };

        // Act
        var result = await _sut.GetForUserAsync(userId, filter);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items.First().Type.Should().Be(NotificationType.QueryCompleted);
    }

    [Fact]
    public async Task GetForUserAsync_WithPriorityFilter_ShouldFilterByPriority()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await _sut.SendAsync(CreateRequest(userId, "Normal", priority: NotificationPriority.Normal));
        await _sut.SendAsync(CreateRequest(userId, "Urgent", priority: NotificationPriority.Urgent));
        await _sut.SendAsync(CreateRequest(userId, "High", priority: NotificationPriority.High));

        var filter = new NotificationFilter { Priority = NotificationPriority.Urgent };

        // Act
        var result = await _sut.GetForUserAsync(userId, filter);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items.First().Priority.Should().Be(NotificationPriority.Urgent);
    }

    [Fact]
    public async Task GetForUserAsync_WithPagination_ShouldReturnCorrectPage()
    {
        // Arrange
        var userId = Guid.NewGuid();
        for (var i = 0; i < 25; i++)
        {
            await _sut.SendAsync(CreateRequest(userId, $"Notification {i}"));
        }

        var filter = new NotificationFilter { PageNumber = 2, PageSize = 10 };

        // Act
        var result = await _sut.GetForUserAsync(userId, filter);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(10);
        result.Value.TotalCount.Should().Be(25);
        result.Value.PageNumber.Should().Be(2);
        result.Value.TotalPages.Should().Be(3);
        result.Value.HasNextPage.Should().BeTrue();
        result.Value.HasPreviousPage.Should().BeTrue();
    }

    [Fact]
    public async Task GetForUserAsync_ShouldExcludeArchivedByDefault()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sendResult = await _sut.SendAsync(CreateRequest(userId, "To Archive"));
        await _sut.SendAsync(CreateRequest(userId, "Normal"));
        await _sut.ArchiveAsync(sendResult.Value.Id);

        // Act
        var result = await _sut.GetForUserAsync(userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items.First().Title.Should().Be("Normal");
    }

    [Fact]
    public async Task GetForUserAsync_WithIncludeArchived_ShouldIncludeArchived()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sendResult = await _sut.SendAsync(CreateRequest(userId, "To Archive"));
        await _sut.SendAsync(CreateRequest(userId, "Normal"));
        await _sut.ArchiveAsync(sendResult.Value.Id);

        var filter = new NotificationFilter { IncludeArchived = true };

        // Act
        var result = await _sut.GetForUserAsync(userId, filter);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
    }

    #endregion

    #region GetUnreadCountAsync Tests

    [Fact]
    public async Task GetUnreadCountAsync_WithNoNotifications_ShouldReturnZero()
    {
        // Act
        var result = await _sut.GetUnreadCountAsync(Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
    }

    [Fact]
    public async Task GetUnreadCountAsync_WithMixedStatus_ShouldCountOnlyUnread()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sendResult1 = await _sut.SendAsync(CreateRequest(userId, "Read"));
        await _sut.SendAsync(CreateRequest(userId, "Unread 1"));
        await _sut.SendAsync(CreateRequest(userId, "Unread 2"));
        await _sut.MarkAsReadAsync(sendResult1.Value.Id);

        // Act
        var result = await _sut.GetUnreadCountAsync(userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(2);
    }

    #endregion

    #region MarkAsReadAsync Tests

    [Fact]
    public async Task MarkAsReadAsync_WithValidId_ShouldMarkAsRead()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sendResult = await _sut.SendAsync(CreateRequest(userId, "To Read"));

        // Act
        var result = await _sut.MarkAsReadAsync(sendResult.Value.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var notification = await _sut.GetByIdAsync(sendResult.Value.Id);
        notification.Value.Status.Should().Be(NotificationStatus.Read);
        notification.Value.ReadAt.Should().NotBeNull();
    }

    [Fact]
    public async Task MarkAsReadAsync_WithNonExistingId_ShouldReturnNotFound()
    {
        // Act
        var result = await _sut.MarkAsReadAsync(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Contain("NotFound");
    }

    [Fact]
    public async Task MarkAsReadAsync_AlreadyRead_ShouldReturnSuccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sendResult = await _sut.SendAsync(CreateRequest(userId, "Already Read"));
        await _sut.MarkAsReadAsync(sendResult.Value.Id);

        // Act
        var result = await _sut.MarkAsReadAsync(sendResult.Value.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region MarkAllAsReadAsync Tests

    [Fact]
    public async Task MarkAllAsReadAsync_WithUnreadNotifications_ShouldMarkAllAsRead()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await _sut.SendAsync(CreateRequest(userId, "Unread 1"));
        await _sut.SendAsync(CreateRequest(userId, "Unread 2"));
        await _sut.SendAsync(CreateRequest(userId, "Unread 3"));

        // Act
        var result = await _sut.MarkAllAsReadAsync(userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(3);

        var unreadCount = await _sut.GetUnreadCountAsync(userId);
        unreadCount.Value.Should().Be(0);
    }

    [Fact]
    public async Task MarkAllAsReadAsync_WithNoNotifications_ShouldReturnZero()
    {
        // Act
        var result = await _sut.MarkAllAsReadAsync(Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
    }

    #endregion

    #region ArchiveAsync Tests

    [Fact]
    public async Task ArchiveAsync_WithValidId_ShouldArchive()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sendResult = await _sut.SendAsync(CreateRequest(userId, "To Archive"));

        // Act
        var result = await _sut.ArchiveAsync(sendResult.Value.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var notification = await _sut.GetByIdAsync(sendResult.Value.Id);
        notification.Value.Status.Should().Be(NotificationStatus.Archived);
        notification.Value.ArchivedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ArchiveAsync_WithNonExistingId_ShouldReturnNotFound()
    {
        // Act
        var result = await _sut.ArchiveAsync(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Contain("NotFound");
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_WithValidId_ShouldDelete()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sendResult = await _sut.SendAsync(CreateRequest(userId, "To Delete"));
        var notificationId = sendResult.Value.Id;

        // Act
        var result = await _sut.DeleteAsync(notificationId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var getResult = await _sut.GetByIdAsync(notificationId);
        getResult.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistingId_ShouldReturnNotFound()
    {
        // Act
        var result = await _sut.DeleteAsync(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Contain("NotFound");
    }

    #endregion

    #region DeleteArchivedBeforeAsync Tests

    [Fact]
    public async Task DeleteArchivedBeforeAsync_WithOldArchived_ShouldDelete()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sendResult = await _sut.SendAsync(CreateRequest(userId, "Old Archived"));
        await _sut.ArchiveAsync(sendResult.Value.Id);

        // Act
        var result = await _sut.DeleteArchivedBeforeAsync(DateTime.UtcNow.AddDays(1));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1);
    }

    [Fact]
    public async Task DeleteArchivedBeforeAsync_WithRecentArchived_ShouldNotDelete()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sendResult = await _sut.SendAsync(CreateRequest(userId, "Recent Archived"));
        await _sut.ArchiveAsync(sendResult.Value.Id);

        // Act
        var result = await _sut.DeleteArchivedBeforeAsync(DateTime.UtcNow.AddDays(-1));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
    }

    #endregion

    #region GetStatsAsync Tests

    [Fact]
    public async Task GetStatsAsync_WithNoNotifications_ShouldReturnEmptyStats()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var result = await _sut.GetStatsAsync(userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(userId);
        result.Value.TotalCount.Should().Be(0);
        result.Value.UnreadCount.Should().Be(0);
        result.Value.ReadCount.Should().Be(0);
        result.Value.ArchivedCount.Should().Be(0);
    }

    [Fact]
    public async Task GetStatsAsync_WithMixedNotifications_ShouldReturnCorrectStats()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Create 3 unread
        await _sut.SendAsync(CreateRequest(userId, "Unread 1"));
        await _sut.SendAsync(CreateRequest(userId, "Unread 2"));
        await _sut.SendAsync(CreateRequest(userId, "Unread 3"));

        // Create and mark as read
        var readResult = await _sut.SendAsync(CreateRequest(userId, "Read 1"));
        await _sut.MarkAsReadAsync(readResult.Value.Id);

        // Create and archive
        var archiveResult = await _sut.SendAsync(CreateRequest(userId, "Archived 1"));
        await _sut.ArchiveAsync(archiveResult.Value.Id);

        // Act
        var result = await _sut.GetStatsAsync(userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(5);
        result.Value.UnreadCount.Should().Be(3);
        result.Value.ReadCount.Should().Be(1);
        result.Value.ArchivedCount.Should().Be(1);
    }

    [Fact]
    public async Task GetStatsAsync_ShouldGroupByType()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await _sut.SendAsync(CreateRequest(userId, "Query 1", NotificationType.QueryCompleted));
        await _sut.SendAsync(CreateRequest(userId, "Query 2", NotificationType.QueryCompleted));
        await _sut.SendAsync(CreateRequest(userId, "Doc", NotificationType.DocumentProcessed));

        // Act
        var result = await _sut.GetStatsAsync(userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CountByType.Should().ContainKey(NotificationType.QueryCompleted);
        result.Value.CountByType[NotificationType.QueryCompleted].Should().Be(2);
        result.Value.CountByType[NotificationType.DocumentProcessed].Should().Be(1);
    }

    [Fact]
    public async Task GetStatsAsync_ShouldGroupByPriority()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await _sut.SendAsync(CreateRequest(userId, "Normal 1", priority: NotificationPriority.Normal));
        await _sut.SendAsync(CreateRequest(userId, "Urgent 1", priority: NotificationPriority.Urgent));
        await _sut.SendAsync(CreateRequest(userId, "Urgent 2", priority: NotificationPriority.Urgent));

        // Act
        var result = await _sut.GetStatsAsync(userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CountByPriority[NotificationPriority.Normal].Should().Be(1);
        result.Value.CountByPriority[NotificationPriority.Urgent].Should().Be(2);
    }

    #endregion

    #region SendToTeamAsync Tests

    [Fact]
    public async Task SendToTeamAsync_WithValidRequest_ShouldSendToTeam()
    {
        // Arrange
        var teamId = Guid.NewGuid();
        var request = new SendNotificationRequest
        {
            UserId = Guid.NewGuid(),
            Type = NotificationType.TeamInvitation,
            Title = "Team Update",
            Message = "Important team update"
        };

        // Act
        var result = await _sut.SendToTeamAsync(teamId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1);
    }

    #endregion

    #region SendToWorkspaceAsync Tests

    [Fact]
    public async Task SendToWorkspaceAsync_WithValidRequest_ShouldSendToWorkspace()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var request = new SendNotificationRequest
        {
            UserId = Guid.NewGuid(),
            Type = NotificationType.ConfigurationChanged,
            Title = "Workspace Update",
            Message = "Important workspace update"
        };

        // Act
        var result = await _sut.SendToWorkspaceAsync(workspaceId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1);
    }

    #endregion

    #region NotificationTemplates Tests

    [Fact]
    public void NotificationTemplates_Welcome_ShouldCreateCorrectNotification()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var request = NotificationTemplates.Welcome(userId, "John");

        // Assert
        request.Type.Should().Be(NotificationType.WelcomeMessage);
        request.Priority.Should().Be(NotificationPriority.Normal);
        request.Title.Should().Contain("Welcome");
    }

    [Fact]
    public void NotificationTemplates_DocumentProcessed_ShouldCreateCorrectNotification()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var documentId = Guid.NewGuid();

        // Act
        var request = NotificationTemplates.DocumentProcessed(
            userId, documentId, "test.pdf", 10, null);

        // Assert
        request.Type.Should().Be(NotificationType.DocumentProcessed);
        request.RelatedEntityId.Should().Be(documentId);
        request.Data.Should().ContainKey("chunkCount");
    }

    [Fact]
    public void NotificationTemplates_QueryCompleted_ShouldCreateCorrectNotification()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var queryId = Guid.NewGuid();

        // Act
        var request = NotificationTemplates.QueryCompleted(
            userId, queryId, "What is AI?", 5, TimeSpan.FromSeconds(2));

        // Assert
        request.Type.Should().Be(NotificationType.QueryCompleted);
        request.RelatedEntityId.Should().Be(queryId);
        request.ActionUrl.Should().Contain(queryId.ToString());
    }

    [Fact]
    public void NotificationTemplates_RateLimitWarning_ShouldCreateHighPriorityNotification()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var request = NotificationTemplates.RateLimitWarning(
            userId, "/api/query", 95, 100);

        // Assert
        request.Type.Should().Be(NotificationType.RateLimitWarning);
        request.Priority.Should().Be(NotificationPriority.High);
    }

    #endregion

    #region Helper Methods

    private static SendNotificationRequest CreateRequest(
        Guid userId,
        string title,
        NotificationType type = NotificationType.Custom,
        NotificationPriority priority = NotificationPriority.Normal)
    {
        return new SendNotificationRequest
        {
            UserId = userId,
            Type = type,
            Title = title,
            Message = $"Message for {title}",
            Priority = priority
        };
    }

    #endregion
}
