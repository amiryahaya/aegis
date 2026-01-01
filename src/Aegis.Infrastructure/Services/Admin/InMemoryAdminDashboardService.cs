using System.Collections.Concurrent;
using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Admin;

/// <summary>
/// In-memory implementation of admin dashboard service for development and testing
/// </summary>
public class InMemoryAdminDashboardService : IAdminDashboardService
{
    private readonly ILogger<InMemoryAdminDashboardService> _logger;
    private readonly ConcurrentDictionary<Guid, SystemAlert> _alerts = new();
    private readonly ConcurrentBag<ActivityFeedItem> _activities = new();
    private readonly DateTime _startTime = DateTime.UtcNow;

    public InMemoryAdminDashboardService(ILogger<InMemoryAdminDashboardService> logger)
    {
        _logger = logger;
    }

    public Task<Result<SystemHealthStatus>> GetSystemHealthAsync(
        CancellationToken cancellationToken = default)
    {
        var components = new Dictionary<string, ComponentHealth>
        {
            ["Database"] = new ComponentHealth
            {
                Name = "Database",
                State = HealthState.Healthy,
                Message = "Connected and responsive",
                LastChecked = DateTime.UtcNow,
                ResponseTime = TimeSpan.FromMilliseconds(5)
            },
            ["VectorStore"] = new ComponentHealth
            {
                Name = "VectorStore",
                State = HealthState.Healthy,
                Message = "Qdrant cluster healthy",
                LastChecked = DateTime.UtcNow,
                ResponseTime = TimeSpan.FromMilliseconds(12)
            },
            ["LLMService"] = new ComponentHealth
            {
                Name = "LLM Service",
                State = HealthState.Healthy,
                Message = "API available",
                LastChecked = DateTime.UtcNow,
                ResponseTime = TimeSpan.FromMilliseconds(150)
            },
            ["Cache"] = new ComponentHealth
            {
                Name = "Cache",
                State = HealthState.Healthy,
                Message = "In-memory cache active",
                LastChecked = DateTime.UtcNow,
                ResponseTime = TimeSpan.FromMilliseconds(1)
            }
        };

        var allHealthy = components.Values.All(c => c.State == HealthState.Healthy);
        var anyUnhealthy = components.Values.Any(c => c.State == HealthState.Unhealthy);

        var status = new SystemHealthStatus
        {
            OverallHealth = anyUnhealthy ? HealthState.Unhealthy
                : allHealthy ? HealthState.Healthy
                : HealthState.Degraded,
            CheckedAt = DateTime.UtcNow,
            Components = components,
            Issues = components.Values
                .Where(c => c.State != HealthState.Healthy)
                .Select(c => $"{c.Name}: {c.Message}")
                .ToList(),
            Uptime = DateTime.UtcNow - _startTime,
            CpuUsagePercent = Random.Shared.NextDouble() * 30 + 10,
            MemoryUsagePercent = Random.Shared.NextDouble() * 40 + 20,
            DiskUsagePercent = Random.Shared.NextDouble() * 20 + 30
        };

        return Task.FromResult(Result.Success(status));
    }

    public Task<Result<DashboardOverview>> GetOverviewAsync(
        DashboardOverviewQuery query,
        CancellationToken cancellationToken = default)
    {
        var overview = new DashboardOverview
        {
            // User metrics
            TotalUsers = 150,
            ActiveUsersToday = 45,
            ActiveUsersThisWeek = 98,
            NewUsersThisMonth = 12,

            // Workspace metrics
            TotalWorkspaces = 25,
            ActiveWorkspaces = 18,

            // Document metrics
            TotalDocuments = 5420,
            TotalDocumentSizeBytes = 2_500_000_000,
            DocumentsProcessedToday = 47,

            // Query metrics
            QueriesThisHour = 156,
            QueriesToday = 2340,
            AverageResponseTimeMs = 450,

            // Cost metrics
            CostToday = 12.50m,
            CostThisMonth = 345.75m,
            TokensUsedToday = 125000,

            // Cache metrics
            CacheHitRate = 68.5,

            // Trends
            UserGrowthTrend = query.IncludeTrends ? 8.5 : null,
            QueryVolumeTrend = query.IncludeTrends ? 12.3 : null,
            CostTrend = query.IncludeTrends ? -5.2 : null,

            GeneratedAt = DateTime.UtcNow
        };

        return Task.FromResult(Result.Success(overview));
    }

    public Task<Result<UserManagementStats>> GetUserStatsAsync(
        Guid? teamId = null,
        CancellationToken cancellationToken = default)
    {
        var stats = new UserManagementStats
        {
            TotalUsers = 150,
            ActiveUsers = 120,
            InactiveUsers = 25,
            PendingUsers = 3,
            DisabledUsers = 2,
            UsersByRole = new Dictionary<string, int>
            {
                ["Admin"] = 5,
                ["Manager"] = 15,
                ["User"] = 120,
                ["Viewer"] = 10
            },
            UsersByTeam = new Dictionary<string, int>
            {
                ["Engineering"] = 45,
                ["Marketing"] = 30,
                ["Sales"] = 35,
                ["Support"] = 25,
                ["Operations"] = 15
            },
            RecentActivity = new List<UserActivitySummary>
            {
                new UserActivitySummary
                {
                    UserId = Guid.NewGuid(),
                    Username = "john.doe",
                    LastActive = DateTime.UtcNow.AddMinutes(-5),
                    QueryCount = 25,
                    DocumentCount = 5
                },
                new UserActivitySummary
                {
                    UserId = Guid.NewGuid(),
                    Username = "jane.smith",
                    LastActive = DateTime.UtcNow.AddMinutes(-15),
                    QueryCount = 18,
                    DocumentCount = 3
                }
            },
            LastUpdated = DateTime.UtcNow
        };

        return Task.FromResult(Result.Success(stats));
    }

    public Task<Result<WorkspaceManagementStats>> GetWorkspaceStatsAsync(
        Guid? teamId = null,
        CancellationToken cancellationToken = default)
    {
        var stats = new WorkspaceManagementStats
        {
            TotalWorkspaces = 25,
            ActiveWorkspaces = 18,
            ArchivedWorkspaces = 7,
            WorkspacesByTeam = new Dictionary<string, int>
            {
                ["Engineering"] = 8,
                ["Marketing"] = 6,
                ["Sales"] = 5,
                ["Support"] = 4,
                ["Operations"] = 2
            },
            TopWorkspaces = new List<WorkspaceSummary>
            {
                new WorkspaceSummary
                {
                    WorkspaceId = Guid.NewGuid(),
                    Name = "Product Documentation",
                    DocumentCount = 450,
                    QueryCount = 5200,
                    UserCount = 35,
                    StorageBytes = 500_000_000,
                    LastActivity = DateTime.UtcNow.AddMinutes(-2)
                },
                new WorkspaceSummary
                {
                    WorkspaceId = Guid.NewGuid(),
                    Name = "Customer Support KB",
                    DocumentCount = 320,
                    QueryCount = 4100,
                    UserCount = 28,
                    StorageBytes = 350_000_000,
                    LastActivity = DateTime.UtcNow.AddMinutes(-5)
                }
            },
            TotalStorageBytes = 2_500_000_000,
            LastUpdated = DateTime.UtcNow
        };

        return Task.FromResult(Result.Success(stats));
    }

    public Task<Result<List<DataSourceHealthInfo>>> GetDataSourceHealthAsync(
        Guid? workspaceId = null,
        CancellationToken cancellationToken = default)
    {
        var dataSources = new List<DataSourceHealthInfo>
        {
            new DataSourceHealthInfo
            {
                DataSourceId = Guid.NewGuid(),
                Name = "SharePoint - Engineering",
                Type = "SharePoint",
                Status = DataSourceStatus.Connected,
                LastSync = DateTime.UtcNow.AddHours(-2),
                NextScheduledSync = DateTime.UtcNow.AddHours(4),
                DocumentCount = 1250,
                PendingDocuments = 5,
                FailedDocuments = 0,
                AverageSyncDuration = TimeSpan.FromMinutes(15)
            },
            new DataSourceHealthInfo
            {
                DataSourceId = Guid.NewGuid(),
                Name = "Google Drive - Marketing",
                Type = "GoogleDrive",
                Status = DataSourceStatus.Syncing,
                LastSync = DateTime.UtcNow.AddHours(-6),
                DocumentCount = 850,
                PendingDocuments = 25,
                FailedDocuments = 2,
                LastError = "Rate limit exceeded on 2 files",
                AverageSyncDuration = TimeSpan.FromMinutes(10)
            },
            new DataSourceHealthInfo
            {
                DataSourceId = Guid.NewGuid(),
                Name = "Confluence - Support",
                Type = "Confluence",
                Status = DataSourceStatus.Connected,
                LastSync = DateTime.UtcNow.AddMinutes(-30),
                NextScheduledSync = DateTime.UtcNow.AddHours(1),
                DocumentCount = 620,
                PendingDocuments = 0,
                FailedDocuments = 0,
                AverageSyncDuration = TimeSpan.FromMinutes(8)
            }
        };

        return Task.FromResult(Result.Success(dataSources));
    }

    public Task<Result<IngestionPipelineStatus>> GetIngestionStatusAsync(
        Guid? workspaceId = null,
        CancellationToken cancellationToken = default)
    {
        var status = new IngestionPipelineStatus
        {
            State = PipelineState.Running,
            QueuedDocuments = 15,
            ProcessingDocuments = 3,
            CompletedToday = 47,
            FailedToday = 2,
            AverageProcessingTimeSeconds = 45,
            ThroughputDocsPerMinute = 1.2,
            ActiveJobs = new List<PipelineJob>
            {
                new PipelineJob
                {
                    JobId = Guid.NewGuid(),
                    DocumentName = "Q4_Report.pdf",
                    Stage = PipelineStage.Embedding,
                    ProgressPercent = 75,
                    StartedAt = DateTime.UtcNow.AddMinutes(-2)
                },
                new PipelineJob
                {
                    JobId = Guid.NewGuid(),
                    DocumentName = "Product_Specs.docx",
                    Stage = PipelineStage.Chunking,
                    ProgressPercent = 40,
                    StartedAt = DateTime.UtcNow.AddMinutes(-1)
                }
            },
            RecentErrors = new List<PipelineError>
            {
                new PipelineError
                {
                    DocumentId = Guid.NewGuid(),
                    DocumentName = "corrupted_file.pdf",
                    ErrorMessage = "Unable to parse PDF: file corrupted",
                    FailedStage = PipelineStage.Parsing,
                    OccurredAt = DateTime.UtcNow.AddHours(-1),
                    RetryCount = 3
                }
            },
            LastUpdated = DateTime.UtcNow
        };

        return Task.FromResult(Result.Success(status));
    }

    public Task<Result<List<SystemAlert>>> GetAlertsAsync(
        AlertQuery query,
        CancellationToken cancellationToken = default)
    {
        var alerts = _alerts.Values.AsEnumerable();

        if (!query.IncludeAcknowledged)
        {
            alerts = alerts.Where(a => !a.IsAcknowledged);
        }

        if (query.MinSeverity.HasValue)
        {
            alerts = alerts.Where(a => a.Severity >= query.MinSeverity);
        }

        if (query.WorkspaceId.HasValue)
        {
            alerts = alerts.Where(a => a.WorkspaceId == query.WorkspaceId);
        }

        var result = alerts
            .OrderByDescending(a => a.Severity)
            .ThenByDescending(a => a.CreatedAt)
            .Take(query.Limit)
            .ToList();

        return Task.FromResult(Result.Success(result));
    }

    public Task<Result<bool>> AcknowledgeAlertAsync(
        Guid alertId,
        Guid acknowledgedBy,
        CancellationToken cancellationToken = default)
    {
        if (!_alerts.TryGetValue(alertId, out var alert))
        {
            return Task.FromResult(Result.Failure<bool>(AdminDashboardErrors.AlertNotFound));
        }

        var updatedAlert = alert with
        {
            IsAcknowledged = true,
            AcknowledgedBy = acknowledgedBy,
            AcknowledgedAt = DateTime.UtcNow
        };

        _alerts[alertId] = updatedAlert;

        _logger.LogInformation("Alert {AlertId} acknowledged by {UserId}", alertId, acknowledgedBy);

        return Task.FromResult(Result.Success(true));
    }

    public Task<Result<List<ActivityFeedItem>>> GetActivityFeedAsync(
        ActivityFeedQuery query,
        CancellationToken cancellationToken = default)
    {
        var activities = _activities.AsEnumerable();

        if (query.WorkspaceId.HasValue)
        {
            activities = activities.Where(a => a.WorkspaceId == query.WorkspaceId);
        }

        if (query.UserId.HasValue)
        {
            activities = activities.Where(a => a.UserId == query.UserId);
        }

        if (query.Types?.Count > 0)
        {
            activities = activities.Where(a => query.Types.Contains(a.Type));
        }

        var result = activities
            .OrderByDescending(a => a.Timestamp)
            .Take(query.Limit)
            .ToList();

        return Task.FromResult(Result.Success(result));
    }

    #region Helper Methods for Testing

    /// <summary>
    /// Create an alert (for testing)
    /// </summary>
    public Task CreateAlertAsync(SystemAlert alert)
    {
        _alerts[alert.Id] = alert;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Add an activity (for testing)
    /// </summary>
    public Task AddActivityAsync(ActivityFeedItem activity)
    {
        _activities.Add(activity);
        return Task.CompletedTask;
    }

    #endregion
}
