using Aegis.Domain.Services;
using Aegis.Infrastructure.Services.Admin;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Aegis.UnitTests.Services.Admin;

public class AuditLogServiceTests
{
    private readonly ILogger<InMemoryAuditLogService> _logger;
    private readonly InMemoryAuditLogService _sut;

    public AuditLogServiceTests()
    {
        _logger = Substitute.For<ILogger<InMemoryAuditLogService>>();
        _sut = new InMemoryAuditLogService(_logger);
    }

    #region LogAsync Tests

    [Fact]
    public async Task LogAsync_WithValidRequest_ReturnsEntry()
    {
        // Arrange
        var request = new AuditLogRequest
        {
            Action = AuditAction.Login,
            Category = AuditCategory.Authentication,
            UserId = Guid.NewGuid(),
            Username = "testuser",
            Description = "User logged in successfully"
        };

        // Act
        var result = await _sut.LogAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Action.Should().Be(AuditAction.Login);
        result.Value.Category.Should().Be(AuditCategory.Authentication);
        result.Value.Username.Should().Be("testuser");
        result.Value.Id.Should().NotBeEmpty();
        result.Value.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task LogAsync_WithFailedAction_RecordsFailure()
    {
        // Arrange
        var request = new AuditLogRequest
        {
            Action = AuditAction.LoginFailed,
            Category = AuditCategory.Authentication,
            UserId = Guid.NewGuid(),
            Success = false,
            ErrorMessage = "Invalid credentials",
            Severity = AuditSeverity.Warning
        };

        // Act
        var result = await _sut.LogAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Success.Should().BeFalse();
        result.Value.ErrorMessage.Should().Be("Invalid credentials");
        result.Value.Severity.Should().Be(AuditSeverity.Warning);
    }

    [Fact]
    public async Task LogAsync_WithMetadata_StoresMetadata()
    {
        // Arrange
        var request = new AuditLogRequest
        {
            Action = AuditAction.DocumentUploaded,
            Category = AuditCategory.DocumentManagement,
            ResourceType = "Document",
            ResourceId = "doc-123",
            Metadata = new Dictionary<string, string>
            {
                { "fileName", "report.pdf" },
                { "fileSize", "1024000" }
            }
        };

        // Act
        var result = await _sut.LogAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Metadata.Should().ContainKey("fileName");
        result.Value.Metadata!["fileName"].Should().Be("report.pdf");
    }

    [Fact]
    public async Task LogAsync_WithOldNewValues_StoresChanges()
    {
        // Arrange
        var request = new AuditLogRequest
        {
            Action = AuditAction.UserUpdated,
            Category = AuditCategory.UserManagement,
            ResourceType = "User",
            ResourceId = "user-123",
            OldValues = new Dictionary<string, object> { { "role", "User" } },
            NewValues = new Dictionary<string, object> { { "role", "Admin" } }
        };

        // Act
        var result = await _sut.LogAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.OldValues.Should().ContainKey("role");
        result.Value.NewValues.Should().ContainKey("role");
        result.Value.OldValues!["role"].Should().Be("User");
        result.Value.NewValues!["role"].Should().Be("Admin");
    }

    #endregion

    #region QueryAsync Tests

    [Fact]
    public async Task QueryAsync_WithNoFilters_ReturnsAllEntries()
    {
        // Arrange
        await CreateTestEntries(10);

        // Act
        var result = await _sut.QueryAsync(new AuditLogQuery());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Entries.Should().HaveCount(10);
        result.Value.TotalCount.Should().Be(10);
    }

    [Fact]
    public async Task QueryAsync_WithActionFilter_ReturnsMatchingEntries()
    {
        // Arrange
        await _sut.LogAsync(new AuditLogRequest { Action = AuditAction.Login, Category = AuditCategory.Authentication });
        await _sut.LogAsync(new AuditLogRequest { Action = AuditAction.Logout, Category = AuditCategory.Authentication });
        await _sut.LogAsync(new AuditLogRequest { Action = AuditAction.Login, Category = AuditCategory.Authentication });

        // Act
        var result = await _sut.QueryAsync(new AuditLogQuery
        {
            Actions = new List<AuditAction> { AuditAction.Login }
        });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Entries.Should().HaveCount(2);
        result.Value.Entries.Should().OnlyContain(e => e.Action == AuditAction.Login);
    }

    [Fact]
    public async Task QueryAsync_WithCategoryFilter_ReturnsMatchingEntries()
    {
        // Arrange
        await _sut.LogAsync(new AuditLogRequest { Action = AuditAction.Login, Category = AuditCategory.Authentication });
        await _sut.LogAsync(new AuditLogRequest { Action = AuditAction.UserCreated, Category = AuditCategory.UserManagement });
        await _sut.LogAsync(new AuditLogRequest { Action = AuditAction.DocumentUploaded, Category = AuditCategory.DocumentManagement });

        // Act
        var result = await _sut.QueryAsync(new AuditLogQuery
        {
            Categories = new List<AuditCategory> { AuditCategory.Authentication, AuditCategory.UserManagement }
        });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Entries.Should().HaveCount(2);
    }

    [Fact]
    public async Task QueryAsync_WithUserIdFilter_ReturnsUserEntries()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await _sut.LogAsync(new AuditLogRequest { Action = AuditAction.Login, Category = AuditCategory.Authentication, UserId = userId });
        await _sut.LogAsync(new AuditLogRequest { Action = AuditAction.Login, Category = AuditCategory.Authentication, UserId = Guid.NewGuid() });

        // Act
        var result = await _sut.QueryAsync(new AuditLogQuery { UserId = userId });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Entries.Should().HaveCount(1);
        result.Value.Entries.First().UserId.Should().Be(userId);
    }

    [Fact]
    public async Task QueryAsync_WithWorkspaceFilter_ReturnsWorkspaceEntries()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        await _sut.LogAsync(new AuditLogRequest { Action = AuditAction.QueryExecuted, Category = AuditCategory.QueryOperations, WorkspaceId = workspaceId });
        await _sut.LogAsync(new AuditLogRequest { Action = AuditAction.QueryExecuted, Category = AuditCategory.QueryOperations, WorkspaceId = Guid.NewGuid() });

        // Act
        var result = await _sut.QueryAsync(new AuditLogQuery { WorkspaceId = workspaceId });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Entries.Should().HaveCount(1);
    }

    [Fact]
    public async Task QueryAsync_WithDateRange_ReturnsEntriesInRange()
    {
        // Arrange - Create entries with specific timestamps
        var svc = new InMemoryAuditLogService(_logger);
        await svc.LogAsync(new AuditLogRequest { Action = AuditAction.Login, Category = AuditCategory.Authentication });

        // Act
        var result = await svc.QueryAsync(new AuditLogQuery
        {
            FromDate = DateTime.UtcNow.AddMinutes(-1),
            ToDate = DateTime.UtcNow.AddMinutes(1)
        });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Entries.Should().HaveCount(1);
    }

    [Fact]
    public async Task QueryAsync_WithSuccessFilter_ReturnsMatchingEntries()
    {
        // Arrange
        await _sut.LogAsync(new AuditLogRequest { Action = AuditAction.Login, Category = AuditCategory.Authentication, Success = true });
        await _sut.LogAsync(new AuditLogRequest { Action = AuditAction.LoginFailed, Category = AuditCategory.Authentication, Success = false });

        // Act
        var result = await _sut.QueryAsync(new AuditLogQuery { Success = false });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Entries.Should().HaveCount(1);
        result.Value.Entries.First().Success.Should().BeFalse();
    }

    [Fact]
    public async Task QueryAsync_WithPagination_ReturnsPaginatedResults()
    {
        // Arrange
        await CreateTestEntries(25);

        // Act
        var result = await _sut.QueryAsync(new AuditLogQuery
        {
            Page = 2,
            PageSize = 10
        });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Entries.Should().HaveCount(10);
        result.Value.Page.Should().Be(2);
        result.Value.PageSize.Should().Be(10);
        result.Value.TotalCount.Should().Be(25);
        result.Value.TotalPages.Should().Be(3);
        result.Value.HasNextPage.Should().BeTrue();
        result.Value.HasPreviousPage.Should().BeTrue();
    }

    [Fact]
    public async Task QueryAsync_WithSearchText_SearchesDescription()
    {
        // Arrange
        await _sut.LogAsync(new AuditLogRequest
        {
            Action = AuditAction.DocumentUploaded,
            Category = AuditCategory.DocumentManagement,
            Description = "Uploaded quarterly report"
        });
        await _sut.LogAsync(new AuditLogRequest
        {
            Action = AuditAction.DocumentUploaded,
            Category = AuditCategory.DocumentManagement,
            Description = "Uploaded annual summary"
        });

        // Act
        var result = await _sut.QueryAsync(new AuditLogQuery { SearchText = "quarterly" });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Entries.Should().HaveCount(1);
        result.Value.Entries.First().Description.Should().Contain("quarterly");
    }

    [Fact]
    public async Task QueryAsync_WithSeverityFilter_ReturnsEntriesAboveThreshold()
    {
        // Arrange
        await _sut.LogAsync(new AuditLogRequest { Action = AuditAction.Login, Category = AuditCategory.Authentication, Severity = AuditSeverity.Info });
        await _sut.LogAsync(new AuditLogRequest { Action = AuditAction.SecurityAlert, Category = AuditCategory.SecurityEvents, Severity = AuditSeverity.Critical });
        await _sut.LogAsync(new AuditLogRequest { Action = AuditAction.LoginFailed, Category = AuditCategory.Authentication, Severity = AuditSeverity.Warning });

        // Act
        var result = await _sut.QueryAsync(new AuditLogQuery { MinSeverity = AuditSeverity.Warning });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Entries.Should().HaveCount(2);
        result.Value.Entries.Should().OnlyContain(e => e.Severity >= AuditSeverity.Warning);
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_WithValidId_ReturnsEntry()
    {
        // Arrange
        var logResult = await _sut.LogAsync(new AuditLogRequest
        {
            Action = AuditAction.Login,
            Category = AuditCategory.Authentication
        });

        // Act
        var result = await _sut.GetByIdAsync(logResult.Value.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(logResult.Value.Id);
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidId_ReturnsNotFound()
    {
        // Act
        var result = await _sut.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
    }

    #endregion

    #region GetStatisticsAsync Tests

    [Fact]
    public async Task GetStatisticsAsync_ReturnsCorrectCounts()
    {
        // Arrange
        await _sut.LogAsync(new AuditLogRequest { Action = AuditAction.Login, Category = AuditCategory.Authentication, UserId = Guid.NewGuid(), Success = true });
        await _sut.LogAsync(new AuditLogRequest { Action = AuditAction.LoginFailed, Category = AuditCategory.Authentication, UserId = Guid.NewGuid(), Success = false });
        await _sut.LogAsync(new AuditLogRequest { Action = AuditAction.Login, Category = AuditCategory.Authentication, UserId = Guid.NewGuid(), Success = true });

        // Act
        var result = await _sut.GetStatisticsAsync(new AuditStatisticsQuery());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalEvents.Should().Be(3);
        result.Value.SuccessfulEvents.Should().Be(2);
        result.Value.FailedEvents.Should().Be(1);
        result.Value.UniqueUsers.Should().Be(3);
    }

    [Fact]
    public async Task GetStatisticsAsync_ReturnsActionBreakdown()
    {
        // Arrange
        await _sut.LogAsync(new AuditLogRequest { Action = AuditAction.Login, Category = AuditCategory.Authentication });
        await _sut.LogAsync(new AuditLogRequest { Action = AuditAction.Login, Category = AuditCategory.Authentication });
        await _sut.LogAsync(new AuditLogRequest { Action = AuditAction.Logout, Category = AuditCategory.Authentication });

        // Act
        var result = await _sut.GetStatisticsAsync(new AuditStatisticsQuery { IncludeActionBreakdown = true });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ActionBreakdown.Should().ContainKey(AuditAction.Login);
        result.Value.ActionBreakdown[AuditAction.Login].Should().Be(2);
        result.Value.ActionBreakdown[AuditAction.Logout].Should().Be(1);
    }

    [Fact]
    public async Task GetStatisticsAsync_ReturnsCategoryBreakdown()
    {
        // Arrange
        await _sut.LogAsync(new AuditLogRequest { Action = AuditAction.Login, Category = AuditCategory.Authentication });
        await _sut.LogAsync(new AuditLogRequest { Action = AuditAction.UserCreated, Category = AuditCategory.UserManagement });
        await _sut.LogAsync(new AuditLogRequest { Action = AuditAction.DocumentUploaded, Category = AuditCategory.DocumentManagement });

        // Act
        var result = await _sut.GetStatisticsAsync(new AuditStatisticsQuery { IncludeCategoryBreakdown = true });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CategoryBreakdown.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetStatisticsAsync_WithWorkspaceFilter_FiltersResults()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        await _sut.LogAsync(new AuditLogRequest { Action = AuditAction.QueryExecuted, Category = AuditCategory.QueryOperations, WorkspaceId = workspaceId });
        await _sut.LogAsync(new AuditLogRequest { Action = AuditAction.QueryExecuted, Category = AuditCategory.QueryOperations, WorkspaceId = Guid.NewGuid() });

        // Act
        var result = await _sut.GetStatisticsAsync(new AuditStatisticsQuery { WorkspaceId = workspaceId });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalEvents.Should().Be(1);
    }

    #endregion

    #region ExportAsync Tests

    [Fact]
    public async Task ExportAsync_ToJson_ReturnsJsonBytes()
    {
        // Arrange
        await CreateTestEntries(5);
        var request = new AuditExportRequest
        {
            Query = new AuditLogQuery(),
            Format = ExportFormat.Json
        };

        // Act
        var result = await _sut.ExportAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        var json = System.Text.Encoding.UTF8.GetString(result.Value);
        json.Should().StartWith("[");
    }

    [Fact]
    public async Task ExportAsync_ToCsv_ReturnsCsvBytes()
    {
        // Arrange
        await CreateTestEntries(5);
        var request = new AuditExportRequest
        {
            Query = new AuditLogQuery(),
            Format = ExportFormat.Csv
        };

        // Act
        var result = await _sut.ExportAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        var csv = System.Text.Encoding.UTF8.GetString(result.Value);
        csv.Should().Contain("Id,Action,Category");
    }

    #endregion

    #region PurgeAsync Tests

    [Fact]
    public async Task PurgeAsync_RemovesOldEntries()
    {
        // Arrange
        await CreateTestEntries(10);

        // Act
        var result = await _sut.PurgeAsync(new AuditPurgeRequest
        {
            OlderThan = DateTime.UtcNow.AddMinutes(1) // Purge everything
        });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(10);

        var remaining = await _sut.QueryAsync(new AuditLogQuery());
        remaining.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task PurgeAsync_WithDryRun_DoesNotDelete()
    {
        // Arrange
        await CreateTestEntries(10);

        // Act
        var result = await _sut.PurgeAsync(new AuditPurgeRequest
        {
            OlderThan = DateTime.UtcNow.AddMinutes(1),
            DryRun = true
        });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(10);

        var remaining = await _sut.QueryAsync(new AuditLogQuery());
        remaining.Value.TotalCount.Should().Be(10);
    }

    [Fact]
    public async Task PurgeAsync_WithSeverityRetention_RetainsCritical()
    {
        // Arrange
        await _sut.LogAsync(new AuditLogRequest { Action = AuditAction.Login, Category = AuditCategory.Authentication, Severity = AuditSeverity.Info });
        await _sut.LogAsync(new AuditLogRequest { Action = AuditAction.SecurityAlert, Category = AuditCategory.SecurityEvents, Severity = AuditSeverity.Critical });

        // Act
        var result = await _sut.PurgeAsync(new AuditPurgeRequest
        {
            OlderThan = DateTime.UtcNow.AddMinutes(1),
            RetainAboveSeverity = AuditSeverity.Error
        });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1); // Only Info was purged

        var remaining = await _sut.QueryAsync(new AuditLogQuery());
        remaining.Value.TotalCount.Should().Be(1);
        remaining.Value.Entries.First().Severity.Should().Be(AuditSeverity.Critical);
    }

    #endregion

    #region Helper Methods

    private async Task CreateTestEntries(int count)
    {
        for (int i = 0; i < count; i++)
        {
            await _sut.LogAsync(new AuditLogRequest
            {
                Action = AuditAction.Login,
                Category = AuditCategory.Authentication,
                UserId = Guid.NewGuid(),
                Description = $"Test entry {i}"
            });
        }
    }

    #endregion
}
