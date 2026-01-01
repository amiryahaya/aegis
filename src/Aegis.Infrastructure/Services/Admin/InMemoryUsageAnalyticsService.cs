using System.Collections.Concurrent;
using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Admin;

/// <summary>
/// In-memory implementation of usage analytics service for development and testing
/// </summary>
public class InMemoryUsageAnalyticsService : IUsageAnalyticsService
{
    private readonly ILogger<InMemoryUsageAnalyticsService> _logger;
    private readonly ConcurrentBag<UsageEvent> _events = new();

    public InMemoryUsageAnalyticsService(ILogger<InMemoryUsageAnalyticsService> logger)
    {
        _logger = logger;
    }

    public Task<Result<UsageEvent>> TrackEventAsync(
        UsageEventRequest request,
        CancellationToken cancellationToken = default)
    {
        var evt = new UsageEvent
        {
            Id = UuidGenerator.NewId(),
            EventType = request.EventType,
            UserId = request.UserId,
            WorkspaceId = request.WorkspaceId,
            TeamId = request.TeamId,
            ResourceType = request.ResourceType,
            ResourceId = request.ResourceId,
            TokensUsed = request.TokensUsed ?? 0,
            InputTokens = request.InputTokens ?? 0,
            OutputTokens = request.OutputTokens ?? 0,
            Cost = request.Cost ?? 0,
            Duration = request.Duration ?? TimeSpan.Zero,
            Model = request.Model,
            CacheHit = request.CacheHit ?? false,
            Metadata = request.Metadata,
            Timestamp = DateTime.UtcNow
        };

        _events.Add(evt);

        _logger.LogDebug(
            "Usage event tracked: {EventType} by user {UserId} in workspace {WorkspaceId}",
            evt.EventType, evt.UserId, evt.WorkspaceId);

        return Task.FromResult(Result.Success(evt));
    }

    public Task<Result<UsageSummary>> GetSummaryAsync(
        UsageSummaryQuery query,
        CancellationToken cancellationToken = default)
    {
        var events = FilterEvents(query.UserId, query.WorkspaceId, query.TeamId,
            query.FromDate, query.ToDate, query.EventTypes);

        var eventList = events.ToList();

        var summary = new UsageSummary
        {
            TotalEvents = eventList.Count,
            TotalQueries = eventList.Count(e => e.EventType == UsageEventType.Query),
            TotalDocuments = eventList.Count(e => e.EventType == UsageEventType.DocumentUpload ||
                                                   e.EventType == UsageEventType.DocumentProcess),
            TotalTokensUsed = eventList.Sum(e => e.TokensUsed),
            TotalInputTokens = eventList.Sum(e => e.InputTokens),
            TotalOutputTokens = eventList.Sum(e => e.OutputTokens),
            TotalCost = eventList.Sum(e => e.Cost),
            TotalDuration = TimeSpan.FromTicks(eventList.Sum(e => e.Duration.Ticks)),
            AverageResponseTimeMs = eventList.Any()
                ? eventList.Average(e => e.Duration.TotalMilliseconds)
                : 0,
            CacheHits = eventList.Count(e => e.CacheHit),
            CacheMisses = eventList.Count(e => !e.CacheHit),
            UniqueUsers = eventList.Where(e => e.UserId.HasValue).Select(e => e.UserId).Distinct().Count(),
            ActiveWorkspaces = eventList.Where(e => e.WorkspaceId.HasValue).Select(e => e.WorkspaceId).Distinct().Count(),
            EventBreakdown = eventList.GroupBy(e => e.EventType).ToDictionary(g => g.Key, g => g.Count()),
            ModelBreakdown = eventList.Where(e => !string.IsNullOrEmpty(e.Model))
                .GroupBy(e => e.Model!).ToDictionary(g => g.Key, g => g.Count()),
            FromDate = query.FromDate ?? eventList.MinBy(e => e.Timestamp)?.Timestamp ?? DateTime.UtcNow,
            ToDate = query.ToDate ?? eventList.MaxBy(e => e.Timestamp)?.Timestamp ?? DateTime.UtcNow
        };

        return Task.FromResult(Result.Success(summary));
    }

    public Task<Result<UsageTrends>> GetTrendsAsync(
        UsageTrendsQuery query,
        CancellationToken cancellationToken = default)
    {
        var events = FilterEvents(query.UserId, query.WorkspaceId, query.TeamId,
            query.FromDate, query.ToDate, null);

        var eventList = events.ToList();

        var dataPoints = query.Granularity switch
        {
            TrendGranularity.Hourly => GroupByHour(eventList),
            TrendGranularity.Daily => GroupByDay(eventList),
            TrendGranularity.Weekly => GroupByWeek(eventList),
            TrendGranularity.Monthly => GroupByMonth(eventList),
            _ => GroupByDay(eventList)
        };

        var metricSummaries = new Dictionary<TrendMetric, TrendSummary>();

        if (query.Metrics.Contains(TrendMetric.Queries))
        {
            var values = dataPoints.Select(d => (double)d.Queries).ToList();
            metricSummaries[TrendMetric.Queries] = CreateTrendSummary(values);
        }

        if (query.Metrics.Contains(TrendMetric.Tokens))
        {
            var values = dataPoints.Select(d => (double)d.Tokens).ToList();
            metricSummaries[TrendMetric.Tokens] = CreateTrendSummary(values);
        }

        if (query.Metrics.Contains(TrendMetric.Cost))
        {
            var values = dataPoints.Select(d => (double)d.Cost).ToList();
            metricSummaries[TrendMetric.Cost] = CreateTrendSummary(values);
        }

        var result = new UsageTrends
        {
            DataPoints = dataPoints,
            Granularity = query.Granularity,
            FromDate = query.FromDate ?? eventList.MinBy(e => e.Timestamp)?.Timestamp ?? DateTime.UtcNow,
            ToDate = query.ToDate ?? eventList.MaxBy(e => e.Timestamp)?.Timestamp ?? DateTime.UtcNow,
            MetricSummaries = metricSummaries
        };

        return Task.FromResult(Result.Success(result));
    }

    public Task<Result<List<UserUsageStats>>> GetTopUsersAsync(
        TopUsersQuery query,
        CancellationToken cancellationToken = default)
    {
        var events = FilterEvents(null, query.WorkspaceId, query.TeamId,
            query.FromDate, query.ToDate, null);

        var userStats = events
            .Where(e => e.UserId.HasValue)
            .GroupBy(e => e.UserId!.Value)
            .Select(g => new UserUsageStats
            {
                UserId = g.Key,
                QueryCount = g.Count(e => e.EventType == UsageEventType.Query),
                DocumentCount = g.Count(e => e.EventType == UsageEventType.DocumentUpload),
                TokensUsed = g.Sum(e => e.TokensUsed),
                Cost = g.Sum(e => e.Cost),
                AverageResponseTimeMs = g.Average(e => e.Duration.TotalMilliseconds),
                LastActive = g.Max(e => e.Timestamp)
            });

        var sorted = query.SortBy switch
        {
            UserSortBy.Tokens => userStats.OrderByDescending(u => u.TokensUsed),
            UserSortBy.Cost => userStats.OrderByDescending(u => u.Cost),
            UserSortBy.Documents => userStats.OrderByDescending(u => u.DocumentCount),
            UserSortBy.LastActive => userStats.OrderByDescending(u => u.LastActive),
            _ => userStats.OrderByDescending(u => u.QueryCount)
        };

        var result = sorted.Take(query.Limit).ToList();

        return Task.FromResult(Result.Success(result));
    }

    public Task<Result<List<WorkspaceUsageStats>>> GetWorkspaceUsageAsync(
        WorkspaceUsageQuery query,
        CancellationToken cancellationToken = default)
    {
        var events = FilterEvents(null, null, query.TeamId, query.FromDate, query.ToDate, null);

        var workspaceStats = events
            .Where(e => e.WorkspaceId.HasValue)
            .GroupBy(e => e.WorkspaceId!.Value)
            .Select(g => new WorkspaceUsageStats
            {
                WorkspaceId = g.Key,
                QueryCount = g.Count(e => e.EventType == UsageEventType.Query),
                DocumentCount = g.Count(e => e.EventType == UsageEventType.DocumentUpload),
                TokensUsed = g.Sum(e => e.TokensUsed),
                Cost = g.Sum(e => e.Cost),
                UniqueUsers = g.Where(e => e.UserId.HasValue).Select(e => e.UserId).Distinct().Count(),
                AverageResponseTimeMs = g.Average(e => e.Duration.TotalMilliseconds)
            });

        var sorted = query.SortBy switch
        {
            WorkspaceSortBy.Tokens => workspaceStats.OrderByDescending(w => w.TokensUsed),
            WorkspaceSortBy.Cost => workspaceStats.OrderByDescending(w => w.Cost),
            WorkspaceSortBy.Documents => workspaceStats.OrderByDescending(w => w.DocumentCount),
            WorkspaceSortBy.Users => workspaceStats.OrderByDescending(w => w.UniqueUsers),
            _ => workspaceStats.OrderByDescending(w => w.QueryCount)
        };

        var result = sorted.Take(query.Limit).ToList();

        return Task.FromResult(Result.Success(result));
    }

    public Task<Result<CostAnalysis>> GetCostAnalysisAsync(
        CostAnalysisQuery query,
        CancellationToken cancellationToken = default)
    {
        var events = FilterEvents(query.UserId, query.WorkspaceId, query.TeamId,
            query.FromDate, query.ToDate, null);

        var eventList = events.ToList();

        var totalCost = eventList.Sum(e => e.Cost);
        var totalTokens = eventList.Sum(e => e.TokensUsed);

        var fromDate = query.FromDate ?? eventList.MinBy(e => e.Timestamp)?.Timestamp ?? DateTime.UtcNow;
        var toDate = query.ToDate ?? eventList.MaxBy(e => e.Timestamp)?.Timestamp ?? DateTime.UtcNow;
        var days = Math.Max(1, (toDate - fromDate).Days);

        var costSavedByCache = eventList
            .Where(e => e.CacheHit && e.Metadata?.ContainsKey("savedCost") == true)
            .Sum(e => decimal.TryParse(e.Metadata!["savedCost"], out var saved) ? saved : 0);

        var result = new CostAnalysis
        {
            TotalCost = totalCost,
            AverageDailyCost = totalCost / days,
            ProjectedMonthlyCost = query.IncludeProjections ? (totalCost / days) * 30 : 0,
            CostByModel = eventList
                .Where(e => !string.IsNullOrEmpty(e.Model))
                .GroupBy(e => e.Model!)
                .ToDictionary(g => g.Key, g => g.Sum(e => e.Cost)),
            CostByEventType = eventList
                .GroupBy(e => e.EventType)
                .ToDictionary(g => g.Key, g => g.Sum(e => e.Cost)),
            DailyCosts = eventList
                .GroupBy(e => e.Timestamp.ToString("yyyy-MM-dd"))
                .ToDictionary(g => g.Key, g => g.Sum(e => e.Cost)),
            CostSavedByCache = costSavedByCache,
            TokensUsed = totalTokens,
            CostPerThousandTokens = totalTokens > 0 ? totalCost / (totalTokens / 1000m) : 0,
            FromDate = fromDate,
            ToDate = toDate
        };

        return Task.FromResult(Result.Success(result));
    }

    public Task<Result<RealTimeMetrics>> GetRealTimeMetricsAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var lastMinute = now.AddMinutes(-1);
        var lastHour = now.AddHours(-1);

        var recentEvents = _events.Where(e => e.Timestamp >= lastHour).ToList();
        var lastMinuteEvents = recentEvents.Where(e => e.Timestamp >= lastMinute).ToList();

        var cacheHits = recentEvents.Count(e => e.CacheHit);
        var cacheMisses = recentEvents.Count(e => !e.CacheHit);

        var result = new RealTimeMetrics
        {
            ActiveUsers = recentEvents.Where(e => e.UserId.HasValue).Select(e => e.UserId).Distinct().Count(),
            QueriesLastMinute = lastMinuteEvents.Count(e => e.EventType == UsageEventType.Query),
            QueriesLastHour = recentEvents.Count(e => e.EventType == UsageEventType.Query),
            AverageResponseTimeMs = recentEvents.Any()
                ? recentEvents.Average(e => e.Duration.TotalMilliseconds)
                : 0,
            PendingRequests = 0, // Would be tracked differently in production
            CacheHitRate = cacheHits + cacheMisses > 0
                ? (double)cacheHits / (cacheHits + cacheMisses) * 100
                : 0,
            CostLastHour = recentEvents.Sum(e => e.Cost),
            TokensLastHour = recentEvents.Sum(e => e.TokensUsed),
            Timestamp = now,
            ActiveUsersByWorkspace = recentEvents
                .Where(e => e.WorkspaceId.HasValue && e.UserId.HasValue)
                .GroupBy(e => e.WorkspaceId!.Value.ToString())
                .ToDictionary(g => g.Key, g => g.Select(e => e.UserId).Distinct().Count())
        };

        return Task.FromResult(Result.Success(result));
    }

    #region Private Methods

    private IEnumerable<UsageEvent> FilterEvents(
        Guid? userId, Guid? workspaceId, Guid? teamId,
        DateTime? fromDate, DateTime? toDate,
        List<UsageEventType>? eventTypes)
    {
        var events = _events.AsEnumerable();

        if (userId.HasValue)
        {
            events = events.Where(e => e.UserId == userId);
        }

        if (workspaceId.HasValue)
        {
            events = events.Where(e => e.WorkspaceId == workspaceId);
        }

        if (teamId.HasValue)
        {
            events = events.Where(e => e.TeamId == teamId);
        }

        if (fromDate.HasValue)
        {
            events = events.Where(e => e.Timestamp >= fromDate);
        }

        if (toDate.HasValue)
        {
            events = events.Where(e => e.Timestamp <= toDate);
        }

        if (eventTypes?.Count > 0)
        {
            events = events.Where(e => eventTypes.Contains(e.EventType));
        }

        return events;
    }

    private List<TrendDataPoint> GroupByHour(List<UsageEvent> events)
    {
        return events
            .GroupBy(e => new DateTime(e.Timestamp.Year, e.Timestamp.Month, e.Timestamp.Day, e.Timestamp.Hour, 0, 0))
            .Select(g => CreateDataPoint(g.Key, g.Key.ToString("yyyy-MM-dd HH:00"), g.ToList()))
            .OrderBy(d => d.Timestamp)
            .ToList();
    }

    private List<TrendDataPoint> GroupByDay(List<UsageEvent> events)
    {
        return events
            .GroupBy(e => e.Timestamp.Date)
            .Select(g => CreateDataPoint(g.Key, g.Key.ToString("yyyy-MM-dd"), g.ToList()))
            .OrderBy(d => d.Timestamp)
            .ToList();
    }

    private List<TrendDataPoint> GroupByWeek(List<UsageEvent> events)
    {
        return events
            .GroupBy(e => GetStartOfWeek(e.Timestamp))
            .Select(g => CreateDataPoint(g.Key, $"Week of {g.Key:yyyy-MM-dd}", g.ToList()))
            .OrderBy(d => d.Timestamp)
            .ToList();
    }

    private List<TrendDataPoint> GroupByMonth(List<UsageEvent> events)
    {
        return events
            .GroupBy(e => new DateTime(e.Timestamp.Year, e.Timestamp.Month, 1))
            .Select(g => CreateDataPoint(g.Key, g.Key.ToString("yyyy-MM"), g.ToList()))
            .OrderBy(d => d.Timestamp)
            .ToList();
    }

    private static DateTime GetStartOfWeek(DateTime date)
    {
        int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.AddDays(-diff).Date;
    }

    private static TrendDataPoint CreateDataPoint(DateTime timestamp, string period, List<UsageEvent> events)
    {
        return new TrendDataPoint
        {
            Timestamp = timestamp,
            Period = period,
            Queries = events.Count(e => e.EventType == UsageEventType.Query),
            Documents = events.Count(e => e.EventType == UsageEventType.DocumentUpload),
            Tokens = events.Sum(e => e.TokensUsed),
            Cost = events.Sum(e => e.Cost),
            AverageResponseTimeMs = events.Any() ? events.Average(e => e.Duration.TotalMilliseconds) : 0,
            CacheHits = events.Count(e => e.CacheHit),
            UniqueUsers = events.Where(e => e.UserId.HasValue).Select(e => e.UserId).Distinct().Count()
        };
    }

    private static TrendSummary CreateTrendSummary(List<double> values)
    {
        if (!values.Any())
        {
            return new TrendSummary
            {
                Total = 0, Average = 0, Min = 0, Max = 0, PercentageChange = 0
            };
        }

        var total = values.Sum();
        var firstHalf = values.Take(values.Count / 2).Sum();
        var secondHalf = values.Skip(values.Count / 2).Sum();
        var percentChange = firstHalf > 0 ? (secondHalf - firstHalf) / firstHalf * 100 : 0;

        return new TrendSummary
        {
            Total = total,
            Average = values.Average(),
            Min = values.Min(),
            Max = values.Max(),
            PercentageChange = percentChange
        };
    }

    #endregion
}
