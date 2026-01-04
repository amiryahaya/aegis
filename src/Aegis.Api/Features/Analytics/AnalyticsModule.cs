using Aegis.Domain.Services;
using Carter;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Aegis.Api.Features.Analytics;

public class AnalyticsModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/analytics")
            .WithTags("Analytics");

        group.MapGet("/summary", GetSummary)
            .WithSummary("Get usage summary for a time period");

        group.MapGet("/trends", GetTrends)
            .WithSummary("Get usage trends over time");

        group.MapGet("/users/top", GetTopUsers)
            .WithSummary("Get top users by usage");

        group.MapGet("/workspaces", GetWorkspaceUsage)
            .WithSummary("Get usage by workspace");

        group.MapGet("/costs", GetCostAnalysis)
            .WithSummary("Get cost analysis for a time period");

        group.MapGet("/realtime", GetRealTimeMetrics)
            .WithSummary("Get real-time usage metrics");

        group.MapPost("/export", ExportAnalytics)
            .WithSummary("Export analytics data to a specific format");
    }

    private static async Task<Results<Ok<UsageSummaryResponse>, BadRequest<ProblemDetails>>>
        GetSummary(
            [FromQuery] Guid? userId,
            [FromQuery] Guid? workspaceId,
            [FromQuery] Guid? teamId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            IUsageAnalyticsService analyticsService,
            CancellationToken cancellationToken)
    {
        var query = new UsageSummaryQuery
        {
            UserId = userId,
            WorkspaceId = workspaceId,
            TeamId = teamId,
            FromDate = fromDate ?? DateTime.UtcNow.AddDays(-30),
            ToDate = toDate ?? DateTime.UtcNow
        };

        var result = await analyticsService.GetSummaryAsync(query, cancellationToken);

        if (result.IsFailure)
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = result.Error!.Code,
                Detail = result.Error.Message
            });
        }

        return TypedResults.Ok(MapToResponse(result.Value));
    }

    private static async Task<Results<Ok<UsageTrendsResponse>, BadRequest<ProblemDetails>>>
        GetTrends(
            [FromQuery] Guid? userId,
            [FromQuery] Guid? workspaceId,
            [FromQuery] Guid? teamId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] string? granularity,
            IUsageAnalyticsService analyticsService,
            CancellationToken cancellationToken)
    {
        var query = new UsageTrendsQuery
        {
            UserId = userId,
            WorkspaceId = workspaceId,
            TeamId = teamId,
            FromDate = fromDate ?? DateTime.UtcNow.AddDays(-30),
            ToDate = toDate ?? DateTime.UtcNow,
            Granularity = ParseGranularity(granularity)
        };

        var result = await analyticsService.GetTrendsAsync(query, cancellationToken);

        if (result.IsFailure)
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = result.Error!.Code,
                Detail = result.Error.Message
            });
        }

        return TypedResults.Ok(MapToResponse(result.Value));
    }

    private static async Task<Results<Ok<List<UserUsageStatsResponse>>, BadRequest<ProblemDetails>>>
        GetTopUsers(
            [FromQuery] Guid? workspaceId,
            [FromQuery] Guid? teamId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] int limit,
            IUsageAnalyticsService analyticsService,
            CancellationToken cancellationToken)
    {
        var query = new TopUsersQuery
        {
            WorkspaceId = workspaceId,
            TeamId = teamId,
            FromDate = fromDate ?? DateTime.UtcNow.AddDays(-30),
            ToDate = toDate ?? DateTime.UtcNow,
            Limit = limit > 0 ? limit : 10
        };

        var result = await analyticsService.GetTopUsersAsync(query, cancellationToken);

        if (result.IsFailure)
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = result.Error!.Code,
                Detail = result.Error.Message
            });
        }

        return TypedResults.Ok(result.Value.Select(MapToResponse).ToList());
    }

    private static async Task<Results<Ok<List<WorkspaceUsageStatsResponse>>, BadRequest<ProblemDetails>>>
        GetWorkspaceUsage(
            [FromQuery] Guid? teamId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] int limit,
            IUsageAnalyticsService analyticsService,
            CancellationToken cancellationToken)
    {
        var query = new WorkspaceUsageQuery
        {
            TeamId = teamId,
            FromDate = fromDate ?? DateTime.UtcNow.AddDays(-30),
            ToDate = toDate ?? DateTime.UtcNow,
            Limit = limit > 0 ? limit : 10
        };

        var result = await analyticsService.GetWorkspaceUsageAsync(query, cancellationToken);

        if (result.IsFailure)
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = result.Error!.Code,
                Detail = result.Error.Message
            });
        }

        return TypedResults.Ok(result.Value.Select(MapToResponse).ToList());
    }

    private static async Task<Results<Ok<CostAnalysisResponse>, BadRequest<ProblemDetails>>>
        GetCostAnalysis(
            [FromQuery] Guid? userId,
            [FromQuery] Guid? workspaceId,
            [FromQuery] Guid? teamId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            IUsageAnalyticsService analyticsService,
            CancellationToken cancellationToken)
    {
        var query = new CostAnalysisQuery
        {
            UserId = userId,
            WorkspaceId = workspaceId,
            TeamId = teamId,
            FromDate = fromDate ?? DateTime.UtcNow.AddDays(-30),
            ToDate = toDate ?? DateTime.UtcNow
        };

        var result = await analyticsService.GetCostAnalysisAsync(query, cancellationToken);

        if (result.IsFailure)
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = result.Error!.Code,
                Detail = result.Error.Message
            });
        }

        return TypedResults.Ok(MapToResponse(result.Value));
    }

    private static async Task<Results<Ok<RealTimeMetricsResponse>, BadRequest<ProblemDetails>>>
        GetRealTimeMetrics(
            IUsageAnalyticsService analyticsService,
            CancellationToken cancellationToken)
    {
        var result = await analyticsService.GetRealTimeMetricsAsync(cancellationToken);

        if (result.IsFailure)
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = result.Error!.Code,
                Detail = result.Error.Message
            });
        }

        return TypedResults.Ok(MapToResponse(result.Value));
    }

    private static async Task<Results<FileContentHttpResult, BadRequest<ProblemDetails>>>
        ExportAnalytics(
            ExportAnalyticsRequest request,
            IUsageAnalyticsService analyticsService,
            CancellationToken cancellationToken)
    {
        var summaryQuery = new UsageSummaryQuery
        {
            UserId = request.UserId,
            WorkspaceId = request.WorkspaceId,
            TeamId = request.TeamId,
            FromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-30),
            ToDate = request.ToDate ?? DateTime.UtcNow
        };

        var trendsQuery = new UsageTrendsQuery
        {
            UserId = request.UserId,
            WorkspaceId = request.WorkspaceId,
            TeamId = request.TeamId,
            FromDate = summaryQuery.FromDate,
            ToDate = summaryQuery.ToDate,
            Granularity = TrendGranularity.Daily
        };

        var summaryResult = await analyticsService.GetSummaryAsync(summaryQuery, cancellationToken);
        var trendsResult = await analyticsService.GetTrendsAsync(trendsQuery, cancellationToken);

        if (summaryResult.IsFailure)
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = summaryResult.Error!.Code,
                Detail = summaryResult.Error.Message
            });
        }

        var exportData = new AnalyticsExportData(
            MapToResponse(summaryResult.Value),
            trendsResult.IsSuccess ? MapToResponse(trendsResult.Value) : null,
            DateTime.UtcNow);

        byte[] content;
        string contentType;
        string extension;

        switch (request.Format.ToLowerInvariant())
        {
            case "json":
                content = System.Text.Encoding.UTF8.GetBytes(
                    System.Text.Json.JsonSerializer.Serialize(exportData, new System.Text.Json.JsonSerializerOptions
                    {
                        WriteIndented = true
                    }));
                contentType = "application/json";
                extension = "json";
                break;
            case "csv":
                content = GenerateAnalyticsCsv(exportData);
                contentType = "text/csv";
                extension = "csv";
                break;
            default:
                content = System.Text.Encoding.UTF8.GetBytes(
                    System.Text.Json.JsonSerializer.Serialize(exportData));
                contentType = "application/json";
                extension = "json";
                break;
        }

        return TypedResults.File(
            content,
            contentType,
            $"analytics-{DateTime.UtcNow:yyyy-MM-dd}.{extension}");
    }

    private static byte[] GenerateAnalyticsCsv(AnalyticsExportData data)
    {
        var sb = new System.Text.StringBuilder();

        // Summary section
        sb.AppendLine("# Analytics Summary");
        sb.AppendLine("Metric,Value");
        sb.AppendLine($"Total Events,{data.Summary.TotalEvents}");
        sb.AppendLine($"Total Queries,{data.Summary.TotalQueries}");
        sb.AppendLine($"Total Documents,{data.Summary.TotalDocuments}");
        sb.AppendLine($"Total Tokens Used,{data.Summary.TotalTokensUsed}");
        sb.AppendLine($"Total Cost,{data.Summary.TotalCost}");
        sb.AppendLine($"Avg Response Time (ms),{data.Summary.AverageResponseTimeMs}");
        sb.AppendLine($"Cache Hit Rate,{data.Summary.CacheHitRate}");
        sb.AppendLine($"Unique Users,{data.Summary.UniqueUsers}");
        sb.AppendLine($"Active Workspaces,{data.Summary.ActiveWorkspaces}");
        sb.AppendLine();

        // Trends section
        if (data.Trends != null)
        {
            sb.AppendLine("# Daily Trends");
            sb.AppendLine("Date,Queries,Documents,Tokens,Cost,Avg Response Time,Unique Users");
            foreach (var point in data.Trends.DataPoints)
            {
                sb.AppendLine($"{point.Period},{point.Queries},{point.Documents},{point.Tokens},{point.Cost},{point.AverageResponseTimeMs},{point.UniqueUsers}");
            }
        }

        return System.Text.Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static TrendGranularity ParseGranularity(string? granularity) =>
        granularity?.ToLowerInvariant() switch
        {
            "hourly" => TrendGranularity.Hourly,
            "daily" => TrendGranularity.Daily,
            "weekly" => TrendGranularity.Weekly,
            "monthly" => TrendGranularity.Monthly,
            _ => TrendGranularity.Daily
        };

    #region Response Mappers

    private static UsageSummaryResponse MapToResponse(UsageSummary summary) =>
        new(
            summary.TotalEvents,
            summary.TotalQueries,
            summary.TotalDocuments,
            summary.TotalTokensUsed,
            summary.TotalCost,
            summary.AverageResponseTimeMs,
            summary.CacheHitRate,
            summary.UniqueUsers,
            summary.ActiveWorkspaces,
            summary.FromDate,
            summary.ToDate);

    private static UsageTrendsResponse MapToResponse(UsageTrends trends) =>
        new(
            trends.DataPoints.Select(d => new TrendDataPointResponse(
                d.Timestamp,
                d.Period,
                d.Queries,
                d.Documents,
                d.Tokens,
                d.Cost,
                d.AverageResponseTimeMs,
                d.UniqueUsers)).ToList(),
            trends.Granularity.ToString(),
            trends.FromDate,
            trends.ToDate);

    private static UserUsageStatsResponse MapToResponse(UserUsageStats stats) =>
        new(
            stats.UserId,
            stats.Username,
            stats.QueryCount,
            stats.DocumentCount,
            stats.TokensUsed,
            stats.Cost,
            stats.AverageResponseTimeMs,
            stats.LastActive);

    private static WorkspaceUsageStatsResponse MapToResponse(WorkspaceUsageStats stats) =>
        new(
            stats.WorkspaceId,
            stats.WorkspaceName,
            stats.QueryCount,
            stats.DocumentCount,
            stats.TokensUsed,
            stats.Cost,
            stats.UniqueUsers,
            stats.AverageResponseTimeMs);

    private static CostAnalysisResponse MapToResponse(CostAnalysis analysis) =>
        new(
            analysis.TotalCost,
            analysis.AverageDailyCost,
            analysis.ProjectedMonthlyCost,
            analysis.CostByModel,
            analysis.DailyCosts,
            analysis.CostSavedByCache,
            analysis.TokensUsed,
            analysis.CostPerThousandTokens,
            analysis.FromDate,
            analysis.ToDate);

    private static RealTimeMetricsResponse MapToResponse(RealTimeMetrics metrics) =>
        new(
            metrics.ActiveUsers,
            metrics.QueriesLastMinute,
            metrics.QueriesLastHour,
            metrics.AverageResponseTimeMs,
            metrics.CacheHitRate,
            metrics.CostLastHour,
            metrics.TokensLastHour,
            metrics.Timestamp);

    #endregion
}

#region Response DTOs

public record UsageSummaryResponse(
    int TotalEvents,
    int TotalQueries,
    int TotalDocuments,
    long TotalTokensUsed,
    decimal TotalCost,
    double AverageResponseTimeMs,
    double CacheHitRate,
    int UniqueUsers,
    int ActiveWorkspaces,
    DateTime FromDate,
    DateTime ToDate);

public record UsageTrendsResponse(
    List<TrendDataPointResponse> DataPoints,
    string Granularity,
    DateTime FromDate,
    DateTime ToDate);

public record TrendDataPointResponse(
    DateTime Timestamp,
    string Period,
    int Queries,
    int Documents,
    long Tokens,
    decimal Cost,
    double AverageResponseTimeMs,
    int UniqueUsers);

public record UserUsageStatsResponse(
    Guid UserId,
    string? Username,
    int QueryCount,
    int DocumentCount,
    long TokensUsed,
    decimal Cost,
    double AverageResponseTimeMs,
    DateTime LastActive);

public record WorkspaceUsageStatsResponse(
    Guid WorkspaceId,
    string? WorkspaceName,
    int QueryCount,
    int DocumentCount,
    long TokensUsed,
    decimal Cost,
    int UniqueUsers,
    double AverageResponseTimeMs);

public record CostAnalysisResponse(
    decimal TotalCost,
    decimal AverageDailyCost,
    decimal ProjectedMonthlyCost,
    Dictionary<string, decimal> CostByModel,
    Dictionary<string, decimal> DailyCosts,
    decimal CostSavedByCache,
    long TokensUsed,
    decimal CostPerThousandTokens,
    DateTime FromDate,
    DateTime ToDate);

public record RealTimeMetricsResponse(
    int ActiveUsers,
    int QueriesLastMinute,
    int QueriesLastHour,
    double AverageResponseTimeMs,
    double CacheHitRate,
    decimal CostLastHour,
    long TokensLastHour,
    DateTime Timestamp);

public record ExportAnalyticsRequest(
    string Format,
    Guid? UserId = null,
    Guid? WorkspaceId = null,
    Guid? TeamId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null);

public record AnalyticsExportData(
    UsageSummaryResponse Summary,
    UsageTrendsResponse? Trends,
    DateTime ExportedAt);

#endregion
