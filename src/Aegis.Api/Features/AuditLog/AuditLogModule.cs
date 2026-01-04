using Aegis.Domain.Services;
using Carter;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Aegis.Api.Features.AuditLog;

public class AuditLogModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/audit")
            .WithTags("Audit Log");

        group.MapGet("/", QueryAuditLogs)
            .WithSummary("Query audit logs with filtering and pagination");

        group.MapGet("/{id:guid}", GetAuditLogEntry)
            .WithSummary("Get a specific audit log entry by ID");

        group.MapGet("/statistics", GetStatistics)
            .WithSummary("Get audit statistics for a time period");

        group.MapPost("/export", ExportAuditLogs)
            .WithSummary("Export audit logs to a specific format");
    }

    private static async Task<Ok<AuditLogQueryResponse>> QueryAuditLogs(
        [FromQuery] string? actions,
        [FromQuery] string? categories,
        [FromQuery] Guid? userId,
        [FromQuery] Guid? workspaceId,
        [FromQuery] Guid? teamId,
        [FromQuery] string? resourceType,
        [FromQuery] bool? success,
        [FromQuery] string? severity,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] string? search,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromQuery] string? sortBy,
        [FromQuery] bool sortDesc,
        IAuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var query = new AuditLogQuery
        {
            Actions = ParseActions(actions),
            Categories = ParseCategories(categories),
            UserId = userId,
            WorkspaceId = workspaceId,
            TeamId = teamId,
            ResourceType = resourceType,
            Success = success,
            MinSeverity = ParseSeverity(severity),
            FromDate = fromDate ?? DateTime.UtcNow.AddDays(-30),
            ToDate = toDate ?? DateTime.UtcNow,
            SearchText = search,
            Page = page > 0 ? page : 1,
            PageSize = pageSize > 0 ? Math.Min(pageSize, 100) : 50,
            SortBy = sortBy ?? "Timestamp",
            SortDescending = sortDesc
        };

        var result = await auditLogService.QueryAsync(query, cancellationToken);

        if (result.IsFailure)
        {
            return TypedResults.Ok(new AuditLogQueryResponse([], 0, query.Page, query.PageSize, 0));
        }

        return TypedResults.Ok(MapToResponse(result.Value));
    }

    private static async Task<Results<Ok<AuditLogEntryResponse>, NotFound>> GetAuditLogEntry(
        Guid id,
        IAuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var result = await auditLogService.GetByIdAsync(id, cancellationToken);

        if (result.IsFailure)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(MapToResponse(result.Value));
    }

    private static async Task<Results<Ok<AuditStatisticsResponse>, BadRequest<ProblemDetails>>>
        GetStatistics(
            [FromQuery] Guid? workspaceId,
            [FromQuery] Guid? teamId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            IAuditLogService auditLogService,
            CancellationToken cancellationToken)
    {
        var query = new AuditStatisticsQuery
        {
            WorkspaceId = workspaceId,
            TeamId = teamId,
            FromDate = fromDate ?? DateTime.UtcNow.AddDays(-30),
            ToDate = toDate ?? DateTime.UtcNow
        };

        var result = await auditLogService.GetStatisticsAsync(query, cancellationToken);

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
        ExportAuditLogs(
            ExportAuditRequest request,
            IAuditLogService auditLogService,
            CancellationToken cancellationToken)
    {
        var query = new AuditLogQuery
        {
            Actions = request.Actions?.Select(a => Enum.Parse<AuditAction>(a)).ToList(),
            Categories = request.Categories?.Select(c => Enum.Parse<AuditCategory>(c)).ToList(),
            UserId = request.UserId,
            WorkspaceId = request.WorkspaceId,
            FromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-30),
            ToDate = request.ToDate ?? DateTime.UtcNow
        };

        var exportRequest = new AuditExportRequest
        {
            Query = query,
            Format = Enum.Parse<ExportFormat>(request.Format, true)
        };

        var result = await auditLogService.ExportAsync(exportRequest, cancellationToken);

        if (result.IsFailure)
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = result.Error!.Code,
                Detail = result.Error.Message
            });
        }

        var contentType = request.Format.ToLowerInvariant() switch
        {
            "json" => "application/json",
            "csv" => "text/csv",
            "pdf" => "application/pdf",
            "excel" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _ => "application/octet-stream"
        };

        var extension = request.Format.ToLowerInvariant() switch
        {
            "json" => "json",
            "csv" => "csv",
            "pdf" => "pdf",
            "excel" => "xlsx",
            _ => "bin"
        };

        return TypedResults.File(
            result.Value,
            contentType,
            $"audit-log-{DateTime.UtcNow:yyyy-MM-dd}.{extension}");
    }

    #region Parsers

    private static List<AuditAction>? ParseActions(string? actions)
    {
        if (string.IsNullOrWhiteSpace(actions)) return null;
        return actions.Split(',')
            .Select(a => Enum.TryParse<AuditAction>(a.Trim(), out var action) ? action : (AuditAction?)null)
            .Where(a => a.HasValue)
            .Select(a => a!.Value)
            .ToList();
    }

    private static List<AuditCategory>? ParseCategories(string? categories)
    {
        if (string.IsNullOrWhiteSpace(categories)) return null;
        return categories.Split(',')
            .Select(c => Enum.TryParse<AuditCategory>(c.Trim(), out var category) ? category : (AuditCategory?)null)
            .Where(c => c.HasValue)
            .Select(c => c!.Value)
            .ToList();
    }

    private static AuditSeverity? ParseSeverity(string? severity)
    {
        if (string.IsNullOrWhiteSpace(severity)) return null;
        return Enum.TryParse<AuditSeverity>(severity, true, out var result) ? result : null;
    }

    #endregion

    #region Response Mappers

    private static AuditLogQueryResponse MapToResponse(AuditLogQueryResult result) =>
        new(
            result.Entries.Select(MapToResponse).ToList(),
            result.TotalCount,
            result.Page,
            result.PageSize,
            result.TotalPages);

    private static AuditLogEntryResponse MapToResponse(AuditLogEntry entry) =>
        new(
            entry.Id,
            entry.Action.ToString(),
            entry.Category.ToString(),
            entry.UserId,
            entry.Username,
            entry.WorkspaceId,
            entry.TeamId,
            entry.ResourceType,
            entry.ResourceId,
            entry.Description,
            entry.IpAddress,
            entry.UserAgent,
            entry.Success,
            entry.ErrorMessage,
            entry.Severity.ToString(),
            entry.CorrelationId,
            entry.Timestamp);

    private static AuditStatisticsResponse MapToResponse(AuditStatistics stats) =>
        new(
            stats.TotalEvents,
            stats.SuccessfulEvents,
            stats.FailedEvents,
            stats.UniqueUsers,
            stats.ActionBreakdown.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
            stats.CategoryBreakdown.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
            stats.SeverityBreakdown.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
            stats.DailyBreakdown,
            stats.FromDate,
            stats.ToDate);

    #endregion
}

#region Request/Response DTOs

public record ExportAuditRequest(
    string Format,
    List<string>? Actions = null,
    List<string>? Categories = null,
    Guid? UserId = null,
    Guid? WorkspaceId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null);

public record AuditLogQueryResponse(
    List<AuditLogEntryResponse> Entries,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public record AuditLogEntryResponse(
    Guid Id,
    string Action,
    string Category,
    Guid? UserId,
    string? Username,
    Guid? WorkspaceId,
    Guid? TeamId,
    string? ResourceType,
    string? ResourceId,
    string? Description,
    string? IpAddress,
    string? UserAgent,
    bool Success,
    string? ErrorMessage,
    string Severity,
    string? CorrelationId,
    DateTime Timestamp);

public record AuditStatisticsResponse(
    int TotalEvents,
    int SuccessfulEvents,
    int FailedEvents,
    int UniqueUsers,
    Dictionary<string, int> ActionBreakdown,
    Dictionary<string, int> CategoryBreakdown,
    Dictionary<string, int> SeverityBreakdown,
    Dictionary<string, int> DailyBreakdown,
    DateTime FromDate,
    DateTime ToDate);

#endregion
