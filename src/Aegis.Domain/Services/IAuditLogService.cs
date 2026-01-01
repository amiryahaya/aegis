using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Service for managing audit logs and compliance tracking
/// </summary>
public interface IAuditLogService
{
    /// <summary>
    /// Log an audit event
    /// </summary>
    Task<Result<AuditLogEntry>> LogAsync(
        AuditLogRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Query audit logs with filtering and pagination
    /// </summary>
    Task<Result<AuditLogQueryResult>> QueryAsync(
        AuditLogQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specific audit log entry by ID
    /// </summary>
    Task<Result<AuditLogEntry>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get audit statistics for a time period
    /// </summary>
    Task<Result<AuditStatistics>> GetStatisticsAsync(
        AuditStatisticsQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Export audit logs to a specific format
    /// </summary>
    Task<Result<byte[]>> ExportAsync(
        AuditExportRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Purge old audit logs based on retention policy
    /// </summary>
    Task<Result<int>> PurgeAsync(
        AuditPurgeRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Archive audit logs before a specific date
    /// </summary>
    /// <param name="beforeDate">Archive logs before this date</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of logs archived</returns>
    Task<Result<int>> ArchiveLogsBeforeAsync(
        DateTime beforeDate,
        CancellationToken cancellationToken = default);
}

#region Request/Response Records

/// <summary>
/// Request to log an audit event
/// </summary>
public record AuditLogRequest
{
    /// <summary>
    /// Type of action being logged
    /// </summary>
    public required AuditAction Action { get; init; }

    /// <summary>
    /// Category of the action
    /// </summary>
    public required AuditCategory Category { get; init; }

    /// <summary>
    /// ID of the user performing the action
    /// </summary>
    public Guid? UserId { get; init; }

    /// <summary>
    /// Username for display purposes
    /// </summary>
    public string? Username { get; init; }

    /// <summary>
    /// ID of the workspace where action occurred
    /// </summary>
    public Guid? WorkspaceId { get; init; }

    /// <summary>
    /// ID of the team if applicable
    /// </summary>
    public Guid? TeamId { get; init; }

    /// <summary>
    /// Type of resource being acted upon
    /// </summary>
    public string? ResourceType { get; init; }

    /// <summary>
    /// ID of the resource being acted upon
    /// </summary>
    public string? ResourceId { get; init; }

    /// <summary>
    /// Description of the action
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// IP address of the client
    /// </summary>
    public string? IpAddress { get; init; }

    /// <summary>
    /// User agent string
    /// </summary>
    public string? UserAgent { get; init; }

    /// <summary>
    /// Whether the action was successful
    /// </summary>
    public bool Success { get; init; } = true;

    /// <summary>
    /// Error message if action failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Previous state before the action (for updates)
    /// </summary>
    public Dictionary<string, object>? OldValues { get; init; }

    /// <summary>
    /// New state after the action (for creates/updates)
    /// </summary>
    public Dictionary<string, object>? NewValues { get; init; }

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, string>? Metadata { get; init; }

    /// <summary>
    /// Severity level of the event
    /// </summary>
    public AuditSeverity Severity { get; init; } = AuditSeverity.Info;

    /// <summary>
    /// Correlation ID for request tracing
    /// </summary>
    public string? CorrelationId { get; init; }
}

/// <summary>
/// A logged audit entry
/// </summary>
public record AuditLogEntry
{
    public required Guid Id { get; init; }
    public required AuditAction Action { get; init; }
    public required AuditCategory Category { get; init; }
    public Guid? UserId { get; init; }
    public string? Username { get; init; }
    public Guid? WorkspaceId { get; init; }
    public Guid? TeamId { get; init; }
    public string? ResourceType { get; init; }
    public string? ResourceId { get; init; }
    public string? Description { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Dictionary<string, object>? OldValues { get; init; }
    public Dictionary<string, object>? NewValues { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
    public AuditSeverity Severity { get; init; }
    public string? CorrelationId { get; init; }
    public required DateTime Timestamp { get; init; }
}

/// <summary>
/// Query for filtering audit logs
/// </summary>
public record AuditLogQuery
{
    /// <summary>
    /// Filter by action types
    /// </summary>
    public List<AuditAction>? Actions { get; init; }

    /// <summary>
    /// Filter by categories
    /// </summary>
    public List<AuditCategory>? Categories { get; init; }

    /// <summary>
    /// Filter by user ID
    /// </summary>
    public Guid? UserId { get; init; }

    /// <summary>
    /// Filter by workspace ID
    /// </summary>
    public Guid? WorkspaceId { get; init; }

    /// <summary>
    /// Filter by team ID
    /// </summary>
    public Guid? TeamId { get; init; }

    /// <summary>
    /// Filter by resource type
    /// </summary>
    public string? ResourceType { get; init; }

    /// <summary>
    /// Filter by resource ID
    /// </summary>
    public string? ResourceId { get; init; }

    /// <summary>
    /// Filter by success/failure
    /// </summary>
    public bool? Success { get; init; }

    /// <summary>
    /// Filter by minimum severity
    /// </summary>
    public AuditSeverity? MinSeverity { get; init; }

    /// <summary>
    /// Start of time range
    /// </summary>
    public DateTime? FromDate { get; init; }

    /// <summary>
    /// End of time range
    /// </summary>
    public DateTime? ToDate { get; init; }

    /// <summary>
    /// Search text in description
    /// </summary>
    public string? SearchText { get; init; }

    /// <summary>
    /// Correlation ID for tracing
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Page number (1-based)
    /// </summary>
    public int Page { get; init; } = 1;

    /// <summary>
    /// Page size
    /// </summary>
    public int PageSize { get; init; } = 50;

    /// <summary>
    /// Sort field
    /// </summary>
    public string SortBy { get; init; } = "Timestamp";

    /// <summary>
    /// Sort direction
    /// </summary>
    public bool SortDescending { get; init; } = true;
}

/// <summary>
/// Result of an audit log query
/// </summary>
public record AuditLogQueryResult
{
    public required List<AuditLogEntry> Entries { get; init; }
    public required int TotalCount { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    public required int TotalPages { get; init; }
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}

/// <summary>
/// Query for audit statistics
/// </summary>
public record AuditStatisticsQuery
{
    public Guid? WorkspaceId { get; init; }
    public Guid? TeamId { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public bool IncludeActionBreakdown { get; init; } = true;
    public bool IncludeCategoryBreakdown { get; init; } = true;
    public bool IncludeUserBreakdown { get; init; } = true;
    public bool IncludeHourlyBreakdown { get; init; } = false;
    public bool IncludeDailyBreakdown { get; init; } = true;
}

/// <summary>
/// Audit statistics result
/// </summary>
public record AuditStatistics
{
    public required int TotalEvents { get; init; }
    public required int SuccessfulEvents { get; init; }
    public required int FailedEvents { get; init; }
    public required int UniqueUsers { get; init; }
    public Dictionary<AuditAction, int> ActionBreakdown { get; init; } = new();
    public Dictionary<AuditCategory, int> CategoryBreakdown { get; init; } = new();
    public Dictionary<string, int> UserBreakdown { get; init; } = new();
    public Dictionary<string, int> HourlyBreakdown { get; init; } = new();
    public Dictionary<string, int> DailyBreakdown { get; init; } = new();
    public Dictionary<AuditSeverity, int> SeverityBreakdown { get; init; } = new();
    public required DateTime FromDate { get; init; }
    public required DateTime ToDate { get; init; }
}

/// <summary>
/// Request to export audit logs
/// </summary>
public record AuditExportRequest
{
    public required AuditLogQuery Query { get; init; }
    public required ExportFormat Format { get; init; }
    public bool IncludeMetadata { get; init; } = true;
    public bool IncludeOldNewValues { get; init; } = false;
    public List<string>? Columns { get; init; }
}

/// <summary>
/// Request to purge old audit logs
/// </summary>
public record AuditPurgeRequest
{
    /// <summary>
    /// Purge entries older than this date
    /// </summary>
    public required DateTime OlderThan { get; init; }

    /// <summary>
    /// Optional workspace filter
    /// </summary>
    public Guid? WorkspaceId { get; init; }

    /// <summary>
    /// Categories to purge (all if not specified)
    /// </summary>
    public List<AuditCategory>? Categories { get; init; }

    /// <summary>
    /// Minimum severity to retain (purge below this level)
    /// </summary>
    public AuditSeverity? RetainAboveSeverity { get; init; }

    /// <summary>
    /// Dry run - don't actually delete
    /// </summary>
    public bool DryRun { get; init; } = false;
}

#endregion

#region Enums

/// <summary>
/// Types of auditable actions
/// </summary>
public enum AuditAction
{
    // Authentication
    Login,
    Logout,
    LoginFailed,
    PasswordChange,
    PasswordReset,
    TokenRefresh,
    MfaEnabled,
    MfaDisabled,

    // User Management
    UserCreated,
    UserUpdated,
    UserDeleted,
    UserEnabled,
    UserDisabled,
    RoleAssigned,
    RoleRevoked,

    // Team Management
    TeamCreated,
    TeamUpdated,
    TeamDeleted,
    MemberAdded,
    MemberRemoved,

    // Workspace Management
    WorkspaceCreated,
    WorkspaceUpdated,
    WorkspaceDeleted,
    WorkspaceAccessGranted,
    WorkspaceAccessRevoked,

    // Data Source Management
    DataSourceCreated,
    DataSourceUpdated,
    DataSourceDeleted,
    DataSourceSynced,
    DataSourceSyncFailed,

    // Document Management
    DocumentUploaded,
    DocumentDeleted,
    DocumentProcessed,
    DocumentProcessingFailed,

    // Query Operations
    QueryExecuted,
    QueryFailed,

    // API Operations
    ApiKeyCreated,
    ApiKeyRevoked,
    ApiKeyRotated,
    RateLimitExceeded,

    // Export Operations
    DataExported,
    ReportGenerated,

    // System Operations
    ConfigurationChanged,
    SystemStarted,
    SystemStopped,
    BackupCreated,
    BackupRestored,

    // Security Events
    SecurityAlert,
    SuspiciousActivity,
    AccessDenied,
    InjectionAttempt
}

/// <summary>
/// Categories of audit events
/// </summary>
public enum AuditCategory
{
    Authentication,
    Authorization,
    UserManagement,
    TeamManagement,
    WorkspaceManagement,
    DataSourceManagement,
    DocumentManagement,
    QueryOperations,
    ApiOperations,
    ExportOperations,
    SystemOperations,
    SecurityEvents
}

/// <summary>
/// Severity levels for audit events
/// </summary>
public enum AuditSeverity
{
    Debug = 0,
    Info = 1,
    Warning = 2,
    Error = 3,
    Critical = 4
}

/// <summary>
/// Export format options
/// </summary>
public enum ExportFormat
{
    Json,
    Csv,
    Pdf,
    Excel
}

#endregion

#region Errors

public static class AuditLogErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "AuditLog.NotFound", "Audit log entry not found");

    public static readonly Error InvalidQuery = Error.Validation(
        "AuditLog.InvalidQuery", "Invalid audit log query parameters");

    public static readonly Error ExportFailed = Error.Internal(
        "AuditLog.ExportFailed", "Failed to export audit logs");

    public static readonly Error PurgeFailed = Error.Internal(
        "AuditLog.PurgeFailed", "Failed to purge audit logs");
}

#endregion
