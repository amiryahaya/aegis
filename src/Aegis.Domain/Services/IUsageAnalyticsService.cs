using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Service for tracking and analyzing usage metrics
/// </summary>
public interface IUsageAnalyticsService
{
    /// <summary>
    /// Track a usage event
    /// </summary>
    Task<Result<UsageEvent>> TrackEventAsync(
        UsageEventRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get usage summary for a time period
    /// </summary>
    Task<Result<UsageSummary>> GetSummaryAsync(
        UsageSummaryQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get usage trends over time
    /// </summary>
    Task<Result<UsageTrends>> GetTrendsAsync(
        UsageTrendsQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get top users by usage
    /// </summary>
    Task<Result<List<UserUsageStats>>> GetTopUsersAsync(
        TopUsersQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get usage by workspace
    /// </summary>
    Task<Result<List<WorkspaceUsageStats>>> GetWorkspaceUsageAsync(
        WorkspaceUsageQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get cost analysis for a time period
    /// </summary>
    Task<Result<CostAnalysis>> GetCostAnalysisAsync(
        CostAnalysisQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get real-time usage metrics
    /// </summary>
    Task<Result<RealTimeMetrics>> GetRealTimeMetricsAsync(
        CancellationToken cancellationToken = default);
}

#region Request/Response Records

/// <summary>
/// Request to track a usage event
/// </summary>
public record UsageEventRequest
{
    public required UsageEventType EventType { get; init; }
    public Guid? UserId { get; init; }
    public Guid? WorkspaceId { get; init; }
    public Guid? TeamId { get; init; }
    public string? ResourceType { get; init; }
    public string? ResourceId { get; init; }
    public int? TokensUsed { get; init; }
    public int? InputTokens { get; init; }
    public int? OutputTokens { get; init; }
    public decimal? Cost { get; init; }
    public TimeSpan? Duration { get; init; }
    public string? Model { get; init; }
    public bool? CacheHit { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// A tracked usage event
/// </summary>
public record UsageEvent
{
    public required Guid Id { get; init; }
    public required UsageEventType EventType { get; init; }
    public Guid? UserId { get; init; }
    public Guid? WorkspaceId { get; init; }
    public Guid? TeamId { get; init; }
    public string? ResourceType { get; init; }
    public string? ResourceId { get; init; }
    public int TokensUsed { get; init; }
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
    public decimal Cost { get; init; }
    public TimeSpan Duration { get; init; }
    public string? Model { get; init; }
    public bool CacheHit { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
    public required DateTime Timestamp { get; init; }
}

/// <summary>
/// Query for usage summary
/// </summary>
public record UsageSummaryQuery
{
    public Guid? UserId { get; init; }
    public Guid? WorkspaceId { get; init; }
    public Guid? TeamId { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public List<UsageEventType>? EventTypes { get; init; }
}

/// <summary>
/// Usage summary result
/// </summary>
public record UsageSummary
{
    public required int TotalEvents { get; init; }
    public required int TotalQueries { get; init; }
    public required int TotalDocuments { get; init; }
    public required long TotalTokensUsed { get; init; }
    public required long TotalInputTokens { get; init; }
    public required long TotalOutputTokens { get; init; }
    public required decimal TotalCost { get; init; }
    public required TimeSpan TotalDuration { get; init; }
    public required double AverageResponseTimeMs { get; init; }
    public required int CacheHits { get; init; }
    public required int CacheMisses { get; init; }
    public double CacheHitRate => CacheHits + CacheMisses > 0
        ? (double)CacheHits / (CacheHits + CacheMisses) * 100
        : 0;
    public required int UniqueUsers { get; init; }
    public required int ActiveWorkspaces { get; init; }
    public Dictionary<UsageEventType, int> EventBreakdown { get; init; } = new();
    public Dictionary<string, int> ModelBreakdown { get; init; } = new();
    public required DateTime FromDate { get; init; }
    public required DateTime ToDate { get; init; }
}

/// <summary>
/// Query for usage trends
/// </summary>
public record UsageTrendsQuery
{
    public Guid? UserId { get; init; }
    public Guid? WorkspaceId { get; init; }
    public Guid? TeamId { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public TrendGranularity Granularity { get; init; } = TrendGranularity.Daily;
    public List<TrendMetric> Metrics { get; init; } = new() { TrendMetric.Queries, TrendMetric.Tokens, TrendMetric.Cost };
}

/// <summary>
/// Usage trends result
/// </summary>
public record UsageTrends
{
    public required List<TrendDataPoint> DataPoints { get; init; }
    public required TrendGranularity Granularity { get; init; }
    public required DateTime FromDate { get; init; }
    public required DateTime ToDate { get; init; }
    public Dictionary<TrendMetric, TrendSummary> MetricSummaries { get; init; } = new();
}

/// <summary>
/// A single data point in a trend
/// </summary>
public record TrendDataPoint
{
    public required DateTime Timestamp { get; init; }
    public required string Period { get; init; }
    public int Queries { get; init; }
    public int Documents { get; init; }
    public long Tokens { get; init; }
    public decimal Cost { get; init; }
    public double AverageResponseTimeMs { get; init; }
    public int CacheHits { get; init; }
    public int UniqueUsers { get; init; }
}

/// <summary>
/// Summary for a trend metric
/// </summary>
public record TrendSummary
{
    public required double Total { get; init; }
    public required double Average { get; init; }
    public required double Min { get; init; }
    public required double Max { get; init; }
    public required double PercentageChange { get; init; }
}

/// <summary>
/// Query for top users
/// </summary>
public record TopUsersQuery
{
    public Guid? WorkspaceId { get; init; }
    public Guid? TeamId { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public int Limit { get; init; } = 10;
    public UserSortBy SortBy { get; init; } = UserSortBy.Queries;
}

/// <summary>
/// User usage statistics
/// </summary>
public record UserUsageStats
{
    public required Guid UserId { get; init; }
    public string? Username { get; init; }
    public required int QueryCount { get; init; }
    public required int DocumentCount { get; init; }
    public required long TokensUsed { get; init; }
    public required decimal Cost { get; init; }
    public required double AverageResponseTimeMs { get; init; }
    public required DateTime LastActive { get; init; }
}

/// <summary>
/// Query for workspace usage
/// </summary>
public record WorkspaceUsageQuery
{
    public Guid? TeamId { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public int Limit { get; init; } = 10;
    public WorkspaceSortBy SortBy { get; init; } = WorkspaceSortBy.Queries;
}

/// <summary>
/// Workspace usage statistics
/// </summary>
public record WorkspaceUsageStats
{
    public required Guid WorkspaceId { get; init; }
    public string? WorkspaceName { get; init; }
    public required int QueryCount { get; init; }
    public required int DocumentCount { get; init; }
    public required long TokensUsed { get; init; }
    public required decimal Cost { get; init; }
    public required int UniqueUsers { get; init; }
    public required double AverageResponseTimeMs { get; init; }
}

/// <summary>
/// Query for cost analysis
/// </summary>
public record CostAnalysisQuery
{
    public Guid? UserId { get; init; }
    public Guid? WorkspaceId { get; init; }
    public Guid? TeamId { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public bool IncludeProjections { get; init; } = true;
}

/// <summary>
/// Cost analysis result
/// </summary>
public record CostAnalysis
{
    public required decimal TotalCost { get; init; }
    public required decimal AverageDailyCost { get; init; }
    public required decimal ProjectedMonthlyCost { get; init; }
    public Dictionary<string, decimal> CostByModel { get; init; } = new();
    public Dictionary<UsageEventType, decimal> CostByEventType { get; init; } = new();
    public Dictionary<string, decimal> DailyCosts { get; init; } = new();
    public required decimal CostSavedByCache { get; init; }
    public required long TokensUsed { get; init; }
    public required decimal CostPerThousandTokens { get; init; }
    public required DateTime FromDate { get; init; }
    public required DateTime ToDate { get; init; }
}

/// <summary>
/// Real-time usage metrics
/// </summary>
public record RealTimeMetrics
{
    public required int ActiveUsers { get; init; }
    public required int QueriesLastMinute { get; init; }
    public required int QueriesLastHour { get; init; }
    public required double AverageResponseTimeMs { get; init; }
    public required int PendingRequests { get; init; }
    public required double CacheHitRate { get; init; }
    public required decimal CostLastHour { get; init; }
    public required long TokensLastHour { get; init; }
    public required DateTime Timestamp { get; init; }
    public Dictionary<string, int> ActiveUsersByWorkspace { get; init; } = new();
}

#endregion

#region Enums

/// <summary>
/// Types of usage events
/// </summary>
public enum UsageEventType
{
    Query,
    DocumentUpload,
    DocumentProcess,
    Embedding,
    Search,
    Chat,
    Export,
    ApiCall,
    CacheHit,
    CacheMiss
}

/// <summary>
/// Granularity for trend analysis
/// </summary>
public enum TrendGranularity
{
    Hourly,
    Daily,
    Weekly,
    Monthly
}

/// <summary>
/// Metrics for trend analysis
/// </summary>
public enum TrendMetric
{
    Queries,
    Documents,
    Tokens,
    Cost,
    ResponseTime,
    CacheHits,
    UniqueUsers
}

/// <summary>
/// Sort options for user stats
/// </summary>
public enum UserSortBy
{
    Queries,
    Tokens,
    Cost,
    Documents,
    LastActive
}

/// <summary>
/// Sort options for workspace stats
/// </summary>
public enum WorkspaceSortBy
{
    Queries,
    Tokens,
    Cost,
    Documents,
    Users
}

#endregion

#region Errors

public static class UsageAnalyticsErrors
{
    public static readonly Error InvalidQuery = Error.Validation(
        "UsageAnalytics.InvalidQuery", "Invalid usage analytics query parameters");

    public static readonly Error NoData = Error.NotFound(
        "UsageAnalytics.NoData", "No usage data found for the specified criteria");
}

#endregion
