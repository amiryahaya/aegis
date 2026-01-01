using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Service for admin dashboard data and operations
/// </summary>
public interface IAdminDashboardService
{
    /// <summary>
    /// Get overall system health status
    /// </summary>
    Task<Result<SystemHealthStatus>> GetSystemHealthAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get dashboard overview metrics
    /// </summary>
    Task<Result<DashboardOverview>> GetOverviewAsync(
        DashboardOverviewQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get user management statistics
    /// </summary>
    Task<Result<UserManagementStats>> GetUserStatsAsync(
        Guid? teamId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get workspace management statistics
    /// </summary>
    Task<Result<WorkspaceManagementStats>> GetWorkspaceStatsAsync(
        Guid? teamId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get data source health and status
    /// </summary>
    Task<Result<List<DataSourceHealthInfo>>> GetDataSourceHealthAsync(
        Guid? workspaceId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get ingestion pipeline status
    /// </summary>
    Task<Result<IngestionPipelineStatus>> GetIngestionStatusAsync(
        Guid? workspaceId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get system alerts and notifications
    /// </summary>
    Task<Result<List<SystemAlert>>> GetAlertsAsync(
        AlertQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Acknowledge an alert
    /// </summary>
    Task<Result<bool>> AcknowledgeAlertAsync(
        Guid alertId,
        Guid acknowledgedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get recent activity feed
    /// </summary>
    Task<Result<List<ActivityFeedItem>>> GetActivityFeedAsync(
        ActivityFeedQuery query,
        CancellationToken cancellationToken = default);
}

#region Request/Response Records

/// <summary>
/// System health status
/// </summary>
public record SystemHealthStatus
{
    public required HealthState OverallHealth { get; init; }
    public required DateTime CheckedAt { get; init; }
    public Dictionary<string, ComponentHealth> Components { get; init; } = new();
    public List<string> Issues { get; init; } = new();
    public TimeSpan Uptime { get; init; }
    public double CpuUsagePercent { get; init; }
    public double MemoryUsagePercent { get; init; }
    public double DiskUsagePercent { get; init; }
}

/// <summary>
/// Health of a system component
/// </summary>
public record ComponentHealth
{
    public required string Name { get; init; }
    public required HealthState State { get; init; }
    public string? Message { get; init; }
    public DateTime LastChecked { get; init; }
    public TimeSpan? ResponseTime { get; init; }
}

/// <summary>
/// Query for dashboard overview
/// </summary>
public record DashboardOverviewQuery
{
    public Guid? TeamId { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public bool IncludeTrends { get; init; } = true;
}

/// <summary>
/// Dashboard overview metrics
/// </summary>
public record DashboardOverview
{
    // User metrics
    public required int TotalUsers { get; init; }
    public required int ActiveUsersToday { get; init; }
    public required int ActiveUsersThisWeek { get; init; }
    public required int NewUsersThisMonth { get; init; }

    // Workspace metrics
    public required int TotalWorkspaces { get; init; }
    public required int ActiveWorkspaces { get; init; }

    // Document metrics
    public required int TotalDocuments { get; init; }
    public required long TotalDocumentSizeBytes { get; init; }
    public required int DocumentsProcessedToday { get; init; }

    // Query metrics
    public required int QueriesThisHour { get; init; }
    public required int QueriesToday { get; init; }
    public required double AverageResponseTimeMs { get; init; }

    // Cost metrics
    public required decimal CostToday { get; init; }
    public required decimal CostThisMonth { get; init; }
    public required long TokensUsedToday { get; init; }

    // Cache metrics
    public required double CacheHitRate { get; init; }

    // Trends (percentage change from previous period)
    public double? UserGrowthTrend { get; init; }
    public double? QueryVolumeTrend { get; init; }
    public double? CostTrend { get; init; }

    public required DateTime GeneratedAt { get; init; }
}

/// <summary>
/// User management statistics
/// </summary>
public record UserManagementStats
{
    public required int TotalUsers { get; init; }
    public required int ActiveUsers { get; init; }
    public required int InactiveUsers { get; init; }
    public required int PendingUsers { get; init; }
    public required int DisabledUsers { get; init; }
    public Dictionary<string, int> UsersByRole { get; init; } = new();
    public Dictionary<string, int> UsersByTeam { get; init; } = new();
    public List<UserActivitySummary> RecentActivity { get; init; } = new();
    public required DateTime LastUpdated { get; init; }
}

/// <summary>
/// Summary of user activity
/// </summary>
public record UserActivitySummary
{
    public required Guid UserId { get; init; }
    public required string Username { get; init; }
    public required DateTime LastActive { get; init; }
    public required int QueryCount { get; init; }
    public required int DocumentCount { get; init; }
}

/// <summary>
/// Workspace management statistics
/// </summary>
public record WorkspaceManagementStats
{
    public required int TotalWorkspaces { get; init; }
    public required int ActiveWorkspaces { get; init; }
    public required int ArchivedWorkspaces { get; init; }
    public Dictionary<string, int> WorkspacesByTeam { get; init; } = new();
    public List<WorkspaceSummary> TopWorkspaces { get; init; } = new();
    public required long TotalStorageBytes { get; init; }
    public required DateTime LastUpdated { get; init; }
}

/// <summary>
/// Summary of workspace
/// </summary>
public record WorkspaceSummary
{
    public required Guid WorkspaceId { get; init; }
    public required string Name { get; init; }
    public required int DocumentCount { get; init; }
    public required int QueryCount { get; init; }
    public required int UserCount { get; init; }
    public required long StorageBytes { get; init; }
    public required DateTime LastActivity { get; init; }
}

/// <summary>
/// Health information for a data source
/// </summary>
public record DataSourceHealthInfo
{
    public required Guid DataSourceId { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required DataSourceStatus Status { get; init; }
    public DateTime? LastSync { get; init; }
    public DateTime? NextScheduledSync { get; init; }
    public int DocumentCount { get; init; }
    public int PendingDocuments { get; init; }
    public int FailedDocuments { get; init; }
    public string? LastError { get; init; }
    public TimeSpan? AverageSyncDuration { get; init; }
}

/// <summary>
/// Ingestion pipeline status
/// </summary>
public record IngestionPipelineStatus
{
    public required PipelineState State { get; init; }
    public required int QueuedDocuments { get; init; }
    public required int ProcessingDocuments { get; init; }
    public required int CompletedToday { get; init; }
    public required int FailedToday { get; init; }
    public required double AverageProcessingTimeSeconds { get; init; }
    public required double ThroughputDocsPerMinute { get; init; }
    public List<PipelineJob> ActiveJobs { get; init; } = new();
    public List<PipelineError> RecentErrors { get; init; } = new();
    public required DateTime LastUpdated { get; init; }
}

/// <summary>
/// A pipeline job
/// </summary>
public record PipelineJob
{
    public required Guid JobId { get; init; }
    public required string DocumentName { get; init; }
    public required PipelineStage Stage { get; init; }
    public required double ProgressPercent { get; init; }
    public required DateTime StartedAt { get; init; }
}

/// <summary>
/// A pipeline error
/// </summary>
public record PipelineError
{
    public required Guid DocumentId { get; init; }
    public required string DocumentName { get; init; }
    public required string ErrorMessage { get; init; }
    public required PipelineStage FailedStage { get; init; }
    public required DateTime OccurredAt { get; init; }
    public int RetryCount { get; init; }
}

/// <summary>
/// Query for alerts
/// </summary>
public record AlertQuery
{
    public AlertSeverity? MinSeverity { get; init; }
    public bool IncludeAcknowledged { get; init; } = false;
    public Guid? WorkspaceId { get; init; }
    public int Limit { get; init; } = 50;
}

/// <summary>
/// A system alert
/// </summary>
public record SystemAlert
{
    public required Guid Id { get; init; }
    public required AlertType Type { get; init; }
    public required AlertSeverity Severity { get; init; }
    public required string Title { get; init; }
    public required string Message { get; init; }
    public Guid? WorkspaceId { get; init; }
    public string? ResourceType { get; init; }
    public string? ResourceId { get; init; }
    public required DateTime CreatedAt { get; init; }
    public bool IsAcknowledged { get; init; }
    public Guid? AcknowledgedBy { get; init; }
    public DateTime? AcknowledgedAt { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// Query for activity feed
/// </summary>
public record ActivityFeedQuery
{
    public Guid? WorkspaceId { get; init; }
    public Guid? UserId { get; init; }
    public List<ActivityType>? Types { get; init; }
    public int Limit { get; init; } = 50;
}

/// <summary>
/// An activity feed item
/// </summary>
public record ActivityFeedItem
{
    public required Guid Id { get; init; }
    public required ActivityType Type { get; init; }
    public required string Description { get; init; }
    public Guid? UserId { get; init; }
    public string? Username { get; init; }
    public Guid? WorkspaceId { get; init; }
    public string? WorkspaceName { get; init; }
    public string? ResourceType { get; init; }
    public string? ResourceId { get; init; }
    public required DateTime Timestamp { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
}

#endregion

#region Enums

/// <summary>
/// Health states
/// </summary>
public enum HealthState
{
    Healthy,
    Degraded,
    Unhealthy,
    Unknown
}

/// <summary>
/// Data source status
/// </summary>
public enum DataSourceStatus
{
    Connected,
    Syncing,
    Error,
    Disconnected,
    Pending
}

/// <summary>
/// Pipeline states
/// </summary>
public enum PipelineState
{
    Running,
    Idle,
    Paused,
    Error
}

/// <summary>
/// Pipeline processing stages
/// </summary>
public enum PipelineStage
{
    Queued,
    Downloading,
    Parsing,
    Chunking,
    Embedding,
    Indexing,
    Completed,
    Failed
}

/// <summary>
/// Alert types
/// </summary>
public enum AlertType
{
    SystemHealth,
    DataSourceError,
    IngestionFailure,
    RateLimitExceeded,
    SecurityEvent,
    StorageWarning,
    CostThreshold,
    UserActivity
}

/// <summary>
/// Alert severity levels
/// </summary>
public enum AlertSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

/// <summary>
/// Activity types for feed
/// </summary>
public enum ActivityType
{
    UserLogin,
    UserCreated,
    DocumentUploaded,
    DocumentProcessed,
    QueryExecuted,
    WorkspaceCreated,
    DataSourceAdded,
    ReportGenerated,
    SettingsChanged
}

#endregion

#region Errors

public static class AdminDashboardErrors
{
    public static readonly Error ServiceUnavailable = Error.Internal(
        "AdminDashboard.ServiceUnavailable", "Dashboard service is unavailable");

    public static readonly Error AlertNotFound = Error.NotFound(
        "AdminDashboard.AlertNotFound", "Alert not found");

    public static readonly Error InsufficientPermissions = Error.Forbidden(
        "AdminDashboard.InsufficientPermissions", "Insufficient permissions for this operation");
}

#endregion
