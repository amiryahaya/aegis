using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.Sessions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Aegis.UnitTests.Services.Sessions;

public class SessionServiceTests
{
    private readonly ILogger<InMemorySessionService> _logger;
    private readonly InMemorySessionService _sut;

    public SessionServiceTests()
    {
        _logger = Substitute.For<ILogger<InMemorySessionService>>();
        _sut = new InMemorySessionService(_logger);
    }

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_WithValidRequest_ShouldCreateSession()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new CreateSessionRequest
        {
            UserId = userId,
            Title = "Test Session",
            Description = "A test session"
        };

        // Act
        var result = await _sut.CreateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(userId);
        result.Value.Title.Should().Be("Test Session");
        result.Value.Status.Should().Be(SessionStatus.Active);
    }

    [Fact]
    public async Task CreateAsync_WithSessionType_ShouldPreserveType()
    {
        // Arrange
        var request = new CreateSessionRequest
        {
            UserId = Guid.NewGuid(),
            Type = SessionType.Research,
            Title = "Research Session"
        };

        // Act
        var result = await _sut.CreateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Type.Should().Be(SessionType.Research);
    }

    [Fact]
    public async Task CreateAsync_WithTags_ShouldPreserveTags()
    {
        // Arrange
        var request = new CreateSessionRequest
        {
            UserId = Guid.NewGuid(),
            Tags = new List<string> { "tag1", "tag2" }
        };

        // Act
        var result = await _sut.CreateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Tags.Should().Contain("tag1");
        result.Value.Tags.Should().Contain("tag2");
    }

    [Fact]
    public async Task CreateAsync_WithSettings_ShouldPreserveSettings()
    {
        // Arrange
        var settings = new SessionSettings
        {
            MaxTurns = 50,
            ContextWindowTurns = 5,
            EnableFollowUps = false
        };

        var request = new CreateSessionRequest
        {
            UserId = Guid.NewGuid(),
            Settings = settings
        };

        // Act
        var result = await _sut.CreateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Settings.MaxTurns.Should().Be(50);
        result.Value.Settings.ContextWindowTurns.Should().Be(5);
        result.Value.Settings.EnableFollowUps.Should().BeFalse();
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_WithExistingSession_ShouldReturnSession()
    {
        // Arrange
        var request = new CreateSessionRequest
        {
            UserId = Guid.NewGuid(),
            Title = "Test Session"
        };
        var createResult = await _sut.CreateAsync(request);

        // Act
        var result = await _sut.GetByIdAsync(createResult.Value.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(createResult.Value.Id);
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
    public async Task GetForUserAsync_WithNoSessions_ShouldReturnEmptyPage()
    {
        // Act
        var result = await _sut.GetForUserAsync(Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetForUserAsync_WithMultipleSessions_ShouldReturnAll()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await _sut.CreateAsync(new CreateSessionRequest { UserId = userId, Title = "Session 1" });
        await _sut.CreateAsync(new CreateSessionRequest { UserId = userId, Title = "Session 2" });
        await _sut.CreateAsync(new CreateSessionRequest { UserId = userId, Title = "Session 3" });

        // Act
        var result = await _sut.GetForUserAsync(userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(3);
        result.Value.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task GetForUserAsync_WithStatusFilter_ShouldFilterByStatus()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var session1 = await _sut.CreateAsync(new CreateSessionRequest { UserId = userId, Title = "Active" });
        var session2 = await _sut.CreateAsync(new CreateSessionRequest { UserId = userId, Title = "Ended" });
        await _sut.EndSessionAsync(session2.Value.Id);

        var filter = new SessionFilter { Status = SessionStatus.Active };

        // Act
        var result = await _sut.GetForUserAsync(userId, filter);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items.First().Title.Should().Be("Active");
    }

    [Fact]
    public async Task GetForUserAsync_WithPagination_ShouldReturnCorrectPage()
    {
        // Arrange
        var userId = Guid.NewGuid();
        for (var i = 0; i < 25; i++)
        {
            await _sut.CreateAsync(new CreateSessionRequest { UserId = userId, Title = $"Session {i}" });
        }

        var filter = new SessionFilter { PageNumber = 2, PageSize = 10 };

        // Act
        var result = await _sut.GetForUserAsync(userId, filter);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(10);
        result.Value.TotalCount.Should().Be(25);
        result.Value.PageNumber.Should().Be(2);
        result.Value.TotalPages.Should().Be(3);
    }

    #endregion

    #region AddTurnAsync Tests

    [Fact]
    public async Task AddTurnAsync_WithValidSession_ShouldAddTurn()
    {
        // Arrange
        var session = await _sut.CreateAsync(new CreateSessionRequest
        {
            UserId = Guid.NewGuid(),
            Title = "Test Session"
        });

        var request = new AddTurnRequest
        {
            Query = "What is AI?"
        };

        // Act
        var result = await _sut.AddTurnAsync(session.Value.Id, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserQuery.Should().Be("What is AI?");
        result.Value.TurnNumber.Should().Be(1);
        result.Value.Status.Should().Be(TurnStatus.Pending);
    }

    [Fact]
    public async Task AddTurnAsync_MultipleTurns_ShouldIncrementTurnNumber()
    {
        // Arrange
        var session = await _sut.CreateAsync(new CreateSessionRequest
        {
            UserId = Guid.NewGuid()
        });

        await _sut.AddTurnAsync(session.Value.Id, new AddTurnRequest { Query = "Query 1" });
        await _sut.AddTurnAsync(session.Value.Id, new AddTurnRequest { Query = "Query 2" });

        // Act
        var result = await _sut.AddTurnAsync(session.Value.Id, new AddTurnRequest { Query = "Query 3" });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TurnNumber.Should().Be(3);
    }

    [Fact]
    public async Task AddTurnAsync_ShouldAutoGenerateTitleFromFirstQuery()
    {
        // Arrange
        var session = await _sut.CreateAsync(new CreateSessionRequest
        {
            UserId = Guid.NewGuid()
            // No title specified
        });

        // Act
        await _sut.AddTurnAsync(session.Value.Id, new AddTurnRequest
        {
            Query = "What is machine learning and how does it work?"
        });

        // Assert
        var updated = await _sut.GetByIdAsync(session.Value.Id);
        updated.Value.Title.Should().Contain("machine learning");
    }

    [Fact]
    public async Task AddTurnAsync_ToEndedSession_ShouldReturnError()
    {
        // Arrange
        var session = await _sut.CreateAsync(new CreateSessionRequest
        {
            UserId = Guid.NewGuid()
        });
        await _sut.EndSessionAsync(session.Value.Id);

        // Act
        var result = await _sut.AddTurnAsync(session.Value.Id, new AddTurnRequest { Query = "Test" });

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Contain("NotActive");
    }

    [Fact]
    public async Task AddTurnAsync_ExceedingMaxTurns_ShouldReturnError()
    {
        // Arrange
        var session = await _sut.CreateAsync(new CreateSessionRequest
        {
            UserId = Guid.NewGuid(),
            Settings = new SessionSettings { MaxTurns = 2 }
        });

        await _sut.AddTurnAsync(session.Value.Id, new AddTurnRequest { Query = "Query 1" });
        await _sut.AddTurnAsync(session.Value.Id, new AddTurnRequest { Query = "Query 2" });

        // Act
        var result = await _sut.AddTurnAsync(session.Value.Id, new AddTurnRequest { Query = "Query 3" });

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Contain("MaxTurns");
    }

    #endregion

    #region GetConversationAsync Tests

    [Fact]
    public async Task GetConversationAsync_ShouldReturnAllTurns()
    {
        // Arrange
        var session = await _sut.CreateAsync(new CreateSessionRequest { UserId = Guid.NewGuid() });
        await _sut.AddTurnAsync(session.Value.Id, new AddTurnRequest { Query = "Query 1" });
        await _sut.AddTurnAsync(session.Value.Id, new AddTurnRequest { Query = "Query 2" });
        await _sut.AddTurnAsync(session.Value.Id, new AddTurnRequest { Query = "Query 3" });

        // Act
        var result = await _sut.GetConversationAsync(session.Value.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetConversationAsync_WithLastN_ShouldReturnLastNTurns()
    {
        // Arrange
        var session = await _sut.CreateAsync(new CreateSessionRequest { UserId = Guid.NewGuid() });
        await _sut.AddTurnAsync(session.Value.Id, new AddTurnRequest { Query = "Query 1" });
        await _sut.AddTurnAsync(session.Value.Id, new AddTurnRequest { Query = "Query 2" });
        await _sut.AddTurnAsync(session.Value.Id, new AddTurnRequest { Query = "Query 3" });

        // Act
        var result = await _sut.GetConversationAsync(session.Value.Id, 2);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.First().UserQuery.Should().Be("Query 2");
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_ShouldUpdateSessionMetadata()
    {
        // Arrange
        var session = await _sut.CreateAsync(new CreateSessionRequest
        {
            UserId = Guid.NewGuid(),
            Title = "Original Title"
        });

        var updateRequest = new UpdateSessionRequest
        {
            Title = "Updated Title",
            Description = "New description"
        };

        // Act
        var result = await _sut.UpdateAsync(session.Value.Id, updateRequest);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Updated Title");
        result.Value.Description.Should().Be("New description");
    }

    [Fact]
    public async Task UpdateTitleAsync_ShouldUpdateTitle()
    {
        // Arrange
        var session = await _sut.CreateAsync(new CreateSessionRequest
        {
            UserId = Guid.NewGuid(),
            Title = "Original"
        });

        // Act
        var result = await _sut.UpdateTitleAsync(session.Value.Id, "New Title");

        // Assert
        result.IsSuccess.Should().BeTrue();
        var updated = await _sut.GetByIdAsync(session.Value.Id);
        updated.Value.Title.Should().Be("New Title");
    }

    #endregion

    #region EndSessionAsync Tests

    [Fact]
    public async Task EndSessionAsync_ShouldEndSession()
    {
        // Arrange
        var session = await _sut.CreateAsync(new CreateSessionRequest { UserId = Guid.NewGuid() });

        // Act
        var result = await _sut.EndSessionAsync(session.Value.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var ended = await _sut.GetByIdAsync(session.Value.Id);
        ended.Value.Status.Should().Be(SessionStatus.Ended);
        ended.Value.EndedAt.Should().NotBeNull();
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_ShouldDeleteSession()
    {
        // Arrange
        var session = await _sut.CreateAsync(new CreateSessionRequest { UserId = Guid.NewGuid() });

        // Act
        var result = await _sut.DeleteAsync(session.Value.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var get = await _sut.GetByIdAsync(session.Value.Id);
        get.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteOlderThanAsync_ShouldDeleteOldEndedSessions()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var session = await _sut.CreateAsync(new CreateSessionRequest { UserId = userId });
        await _sut.EndSessionAsync(session.Value.Id);

        // Act
        var result = await _sut.DeleteOlderThanAsync(DateTime.UtcNow.AddDays(1));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1);
    }

    #endregion

    #region GetStatsAsync Tests

    [Fact]
    public async Task GetStatsAsync_WithNoSessions_ShouldReturnEmptyStats()
    {
        // Act
        var result = await _sut.GetStatsAsync(Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalSessions.Should().Be(0);
    }

    [Fact]
    public async Task GetStatsAsync_ShouldCalculateCorrectStats()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Create active session with turns
        var session1 = await _sut.CreateAsync(new CreateSessionRequest
        {
            UserId = userId,
            Type = SessionType.Query
        });
        await _sut.AddTurnAsync(session1.Value.Id, new AddTurnRequest { Query = "Q1" });
        await _sut.AddTurnAsync(session1.Value.Id, new AddTurnRequest { Query = "Q2" });

        // Create ended session
        var session2 = await _sut.CreateAsync(new CreateSessionRequest
        {
            UserId = userId,
            Type = SessionType.Research
        });
        await _sut.EndSessionAsync(session2.Value.Id);

        // Act
        var result = await _sut.GetStatsAsync(userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalSessions.Should().Be(2);
        result.Value.ActiveSessions.Should().Be(1);
        result.Value.TotalTurns.Should().Be(2);
        result.Value.SessionsByType.Should().ContainKey(SessionType.Query);
        result.Value.SessionsByType.Should().ContainKey(SessionType.Research);
    }

    #endregion

    #region ShareAsync Tests

    [Fact]
    public async Task ShareAsync_WithUser_ShouldCreateShare()
    {
        // Arrange
        var session = await _sut.CreateAsync(new CreateSessionRequest { UserId = Guid.NewGuid() });
        var shareWithUserId = Guid.NewGuid();

        var request = new ShareSessionRequest
        {
            ShareWithUserId = shareWithUserId,
            Permission = SessionSharePermission.ReadOnly
        };

        // Act
        var result = await _sut.ShareAsync(session.Value.Id, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SharedWithUserId.Should().Be(shareWithUserId);
        result.Value.Permission.Should().Be(SessionSharePermission.ReadOnly);
    }

    [Fact]
    public async Task ShareAsync_PublicShare_ShouldGenerateShareLink()
    {
        // Arrange
        var session = await _sut.CreateAsync(new CreateSessionRequest { UserId = Guid.NewGuid() });

        var request = new ShareSessionRequest
        {
            IsPublic = true
        };

        // Act
        var result = await _sut.ShareAsync(session.Value.Id, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsPublic.Should().BeTrue();
        result.Value.ShareLink.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetSharedWithUserAsync_ShouldReturnSharedSessions()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var sharedWithUserId = Guid.NewGuid();

        var session = await _sut.CreateAsync(new CreateSessionRequest { UserId = ownerId, Title = "Shared" });
        await _sut.ShareAsync(session.Value.Id, new ShareSessionRequest { ShareWithUserId = sharedWithUserId });

        // Act
        var result = await _sut.GetSharedWithUserAsync(sharedWithUserId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value.First().Title.Should().Be("Shared");
    }

    #endregion

    #region ExportAsync Tests

    [Fact]
    public async Task ExportAsync_ToJson_ShouldExportSession()
    {
        // Arrange
        var session = await _sut.CreateAsync(new CreateSessionRequest
        {
            UserId = Guid.NewGuid(),
            Title = "Export Test"
        });
        await _sut.AddTurnAsync(session.Value.Id, new AddTurnRequest { Query = "Test query" });

        // Act
        var result = await _sut.ExportAsync(session.Value.Id, SessionExportFormat.Json);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Format.Should().Be(SessionExportFormat.Json);
        result.Value.ContentType.Should().Be("application/json");
        result.Value.Content.Should().Contain("Export Test");
    }

    [Fact]
    public async Task ExportAsync_ToMarkdown_ShouldExportSession()
    {
        // Arrange
        var session = await _sut.CreateAsync(new CreateSessionRequest
        {
            UserId = Guid.NewGuid(),
            Title = "Export Test"
        });
        await _sut.AddTurnAsync(session.Value.Id, new AddTurnRequest { Query = "Test query" });

        // Act
        var result = await _sut.ExportAsync(session.Value.Id, SessionExportFormat.Markdown);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Format.Should().Be(SessionExportFormat.Markdown);
        result.Value.ContentType.Should().Be("text/markdown");
        result.Value.Content.Should().Contain("# Export Test");
    }

    #endregion

    #region AddTurnFeedbackAsync Tests

    [Fact]
    public async Task AddTurnFeedbackAsync_ShouldAddFeedback()
    {
        // Arrange
        var session = await _sut.CreateAsync(new CreateSessionRequest { UserId = Guid.NewGuid() });
        var turn = await _sut.AddTurnAsync(session.Value.Id, new AddTurnRequest { Query = "Test" });

        var feedback = new TurnFeedback
        {
            Rating = FeedbackRating.Positive,
            Comment = "Very helpful!"
        };

        // Act
        var result = await _sut.AddTurnFeedbackAsync(session.Value.Id, turn.Value.Id, feedback);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var conversation = await _sut.GetConversationAsync(session.Value.Id);
        conversation.Value.First().Feedback.Should().NotBeNull();
        conversation.Value.First().Feedback!.Rating.Should().Be(FeedbackRating.Positive);
    }

    #endregion

    #region SearchAsync Tests

    [Fact]
    public async Task SearchAsync_ShouldFindSessionsByQuery()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var session = await _sut.CreateAsync(new CreateSessionRequest
        {
            UserId = userId,
            Title = "Machine Learning Research"
        });
        await _sut.AddTurnAsync(session.Value.Id, new AddTurnRequest { Query = "What is deep learning?" });

        // Act
        var result = await _sut.SearchAsync("deep learning", userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task SearchAsync_ShouldSearchInTurnContent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var session = await _sut.CreateAsync(new CreateSessionRequest { UserId = userId, Title = "AI Session" });
        await _sut.AddTurnAsync(session.Value.Id, new AddTurnRequest { Query = "Explain neural networks" });

        // Act
        var result = await _sut.SearchAsync("neural", userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
    }

    #endregion

    #region SessionTemplates Tests

    [Fact]
    public void SessionTemplates_QuickQuery_ShouldCreateCorrectRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var request = SessionTemplates.QuickQuery(userId);

        // Assert
        request.UserId.Should().Be(userId);
        request.Type.Should().Be(SessionType.Query);
        request.Settings!.MaxTurns.Should().Be(10);
    }

    [Fact]
    public void SessionTemplates_ResearchSession_ShouldCreateCorrectRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var teamId = Guid.NewGuid();

        // Act
        var request = SessionTemplates.ResearchSession(userId, "AI Ethics", teamId);

        // Assert
        request.UserId.Should().Be(userId);
        request.TeamId.Should().Be(teamId);
        request.Type.Should().Be(SessionType.Research);
        request.Title.Should().Contain("AI Ethics");
    }

    #endregion

    #region GetActiveSessionsAsync Tests

    [Fact]
    public async Task GetActiveSessionsAsync_ShouldReturnOnlyActiveSessions()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await _sut.CreateAsync(new CreateSessionRequest { UserId = userId, Title = "Active 1" });
        var session2 = await _sut.CreateAsync(new CreateSessionRequest { UserId = userId, Title = "Ended" });
        await _sut.CreateAsync(new CreateSessionRequest { UserId = userId, Title = "Active 2" });
        await _sut.EndSessionAsync(session2.Value.Id);

        // Act
        var result = await _sut.GetActiveSessionsAsync(userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.All(s => s.Status == SessionStatus.Active).Should().BeTrue();
    }

    #endregion
}
