using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Admin;

/// <summary>
/// In-memory implementation of audit log service for development and testing
/// </summary>
public class InMemoryAuditLogService : IAuditLogService
{
    private readonly ILogger<InMemoryAuditLogService> _logger;
    private readonly ConcurrentDictionary<Guid, AuditLogEntry> _entries = new();
    private readonly object _lock = new();

    public InMemoryAuditLogService(ILogger<InMemoryAuditLogService> logger)
    {
        _logger = logger;
    }

    public Task<Result<AuditLogEntry>> LogAsync(
        AuditLogRequest request,
        CancellationToken cancellationToken = default)
    {
        var entry = new AuditLogEntry
        {
            Id = UuidGenerator.NewId(),
            Action = request.Action,
            Category = request.Category,
            UserId = request.UserId,
            Username = request.Username,
            WorkspaceId = request.WorkspaceId,
            TeamId = request.TeamId,
            ResourceType = request.ResourceType,
            ResourceId = request.ResourceId,
            Description = request.Description,
            IpAddress = request.IpAddress,
            UserAgent = request.UserAgent,
            Success = request.Success,
            ErrorMessage = request.ErrorMessage,
            OldValues = request.OldValues,
            NewValues = request.NewValues,
            Metadata = request.Metadata,
            Severity = request.Severity,
            CorrelationId = request.CorrelationId,
            Timestamp = DateTime.UtcNow
        };

        _entries[entry.Id] = entry;

        _logger.LogDebug(
            "Audit log entry created: {Action} by {Username} on {ResourceType}/{ResourceId}",
            entry.Action, entry.Username, entry.ResourceType, entry.ResourceId);

        return Task.FromResult(Result.Success(entry));
    }

    public Task<Result<AuditLogQueryResult>> QueryAsync(
        AuditLogQuery query,
        CancellationToken cancellationToken = default)
    {
        var entries = _entries.Values.AsEnumerable();

        // Apply filters
        if (query.Actions?.Count > 0)
        {
            entries = entries.Where(e => query.Actions.Contains(e.Action));
        }

        if (query.Categories?.Count > 0)
        {
            entries = entries.Where(e => query.Categories.Contains(e.Category));
        }

        if (query.UserId.HasValue)
        {
            entries = entries.Where(e => e.UserId == query.UserId);
        }

        if (query.WorkspaceId.HasValue)
        {
            entries = entries.Where(e => e.WorkspaceId == query.WorkspaceId);
        }

        if (query.TeamId.HasValue)
        {
            entries = entries.Where(e => e.TeamId == query.TeamId);
        }

        if (!string.IsNullOrEmpty(query.ResourceType))
        {
            entries = entries.Where(e => e.ResourceType == query.ResourceType);
        }

        if (!string.IsNullOrEmpty(query.ResourceId))
        {
            entries = entries.Where(e => e.ResourceId == query.ResourceId);
        }

        if (query.Success.HasValue)
        {
            entries = entries.Where(e => e.Success == query.Success);
        }

        if (query.MinSeverity.HasValue)
        {
            entries = entries.Where(e => e.Severity >= query.MinSeverity);
        }

        if (query.FromDate.HasValue)
        {
            entries = entries.Where(e => e.Timestamp >= query.FromDate);
        }

        if (query.ToDate.HasValue)
        {
            entries = entries.Where(e => e.Timestamp <= query.ToDate);
        }

        if (!string.IsNullOrEmpty(query.SearchText))
        {
            var searchLower = query.SearchText.ToLowerInvariant();
            entries = entries.Where(e =>
                (e.Description?.ToLowerInvariant().Contains(searchLower) ?? false) ||
                (e.Username?.ToLowerInvariant().Contains(searchLower) ?? false) ||
                (e.ResourceType?.ToLowerInvariant().Contains(searchLower) ?? false) ||
                (e.ResourceId?.ToLowerInvariant().Contains(searchLower) ?? false));
        }

        if (!string.IsNullOrEmpty(query.CorrelationId))
        {
            entries = entries.Where(e => e.CorrelationId == query.CorrelationId);
        }

        // Count total before pagination
        var totalCount = entries.Count();

        // Apply sorting
        entries = query.SortBy?.ToLowerInvariant() switch
        {
            "action" => query.SortDescending
                ? entries.OrderByDescending(e => e.Action)
                : entries.OrderBy(e => e.Action),
            "category" => query.SortDescending
                ? entries.OrderByDescending(e => e.Category)
                : entries.OrderBy(e => e.Category),
            "severity" => query.SortDescending
                ? entries.OrderByDescending(e => e.Severity)
                : entries.OrderBy(e => e.Severity),
            _ => query.SortDescending
                ? entries.OrderByDescending(e => e.Timestamp)
                : entries.OrderBy(e => e.Timestamp)
        };

        // Apply pagination
        var pageSize = Math.Max(1, Math.Min(query.PageSize, 1000));
        var page = Math.Max(1, query.Page);
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        entries = entries
            .Skip((page - 1) * pageSize)
            .Take(pageSize);

        var result = new AuditLogQueryResult
        {
            Entries = entries.ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages
        };

        return Task.FromResult(Result.Success(result));
    }

    public Task<Result<AuditLogEntry>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (_entries.TryGetValue(id, out var entry))
        {
            return Task.FromResult(Result.Success(entry));
        }

        return Task.FromResult(Result.Failure<AuditLogEntry>(AuditLogErrors.NotFound));
    }

    public Task<Result<AuditStatistics>> GetStatisticsAsync(
        AuditStatisticsQuery query,
        CancellationToken cancellationToken = default)
    {
        var entries = _entries.Values.AsEnumerable();

        // Apply filters
        if (query.WorkspaceId.HasValue)
        {
            entries = entries.Where(e => e.WorkspaceId == query.WorkspaceId);
        }

        if (query.TeamId.HasValue)
        {
            entries = entries.Where(e => e.TeamId == query.TeamId);
        }

        if (query.FromDate.HasValue)
        {
            entries = entries.Where(e => e.Timestamp >= query.FromDate);
        }

        if (query.ToDate.HasValue)
        {
            entries = entries.Where(e => e.Timestamp <= query.ToDate);
        }

        var entryList = entries.ToList();

        var stats = new AuditStatistics
        {
            TotalEvents = entryList.Count,
            SuccessfulEvents = entryList.Count(e => e.Success),
            FailedEvents = entryList.Count(e => !e.Success),
            UniqueUsers = entryList.Where(e => e.UserId.HasValue).Select(e => e.UserId).Distinct().Count(),
            FromDate = query.FromDate ?? entryList.MinBy(e => e.Timestamp)?.Timestamp ?? DateTime.UtcNow,
            ToDate = query.ToDate ?? entryList.MaxBy(e => e.Timestamp)?.Timestamp ?? DateTime.UtcNow
        };

        if (query.IncludeActionBreakdown)
        {
            stats = stats with
            {
                ActionBreakdown = entryList
                    .GroupBy(e => e.Action)
                    .ToDictionary(g => g.Key, g => g.Count())
            };
        }

        if (query.IncludeCategoryBreakdown)
        {
            stats = stats with
            {
                CategoryBreakdown = entryList
                    .GroupBy(e => e.Category)
                    .ToDictionary(g => g.Key, g => g.Count())
            };
        }

        if (query.IncludeUserBreakdown)
        {
            stats = stats with
            {
                UserBreakdown = entryList
                    .Where(e => !string.IsNullOrEmpty(e.Username))
                    .GroupBy(e => e.Username!)
                    .ToDictionary(g => g.Key, g => g.Count())
            };
        }

        if (query.IncludeHourlyBreakdown)
        {
            stats = stats with
            {
                HourlyBreakdown = entryList
                    .GroupBy(e => e.Timestamp.ToString("yyyy-MM-dd HH:00"))
                    .ToDictionary(g => g.Key, g => g.Count())
            };
        }

        if (query.IncludeDailyBreakdown)
        {
            stats = stats with
            {
                DailyBreakdown = entryList
                    .GroupBy(e => e.Timestamp.ToString("yyyy-MM-dd"))
                    .ToDictionary(g => g.Key, g => g.Count())
            };
        }

        stats = stats with
        {
            SeverityBreakdown = entryList
                .GroupBy(e => e.Severity)
                .ToDictionary(g => g.Key, g => g.Count())
        };

        return Task.FromResult(Result.Success(stats));
    }

    public Task<Result<byte[]>> ExportAsync(
        AuditExportRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var queryResult = QueryAsync(request.Query with { PageSize = int.MaxValue }, cancellationToken).Result;
            if (queryResult.IsFailure)
            {
                return Task.FromResult(Result.Failure<byte[]>(AuditLogErrors.ExportFailed));
            }

            var entries = queryResult.Value.Entries;

            byte[] result = request.Format switch
            {
                ExportFormat.Json => ExportToJson(entries, request),
                ExportFormat.Csv => ExportToCsv(entries, request),
                _ => throw new NotSupportedException($"Export format {request.Format} is not supported")
            };

            return Task.FromResult(Result.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export audit logs");
            return Task.FromResult(Result.Failure<byte[]>(AuditLogErrors.ExportFailed));
        }
    }

    public Task<Result<int>> PurgeAsync(
        AuditPurgeRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entriesToPurge = _entries.Values
                .Where(e => e.Timestamp < request.OlderThan)
                .AsEnumerable();

            if (request.WorkspaceId.HasValue)
            {
                entriesToPurge = entriesToPurge.Where(e => e.WorkspaceId == request.WorkspaceId);
            }

            if (request.Categories?.Count > 0)
            {
                entriesToPurge = entriesToPurge.Where(e => request.Categories.Contains(e.Category));
            }

            if (request.RetainAboveSeverity.HasValue)
            {
                entriesToPurge = entriesToPurge.Where(e => e.Severity < request.RetainAboveSeverity);
            }

            var purgeList = entriesToPurge.ToList();
            var count = purgeList.Count;

            if (!request.DryRun)
            {
                foreach (var entry in purgeList)
                {
                    _entries.TryRemove(entry.Id, out _);
                }

                _logger.LogInformation("Purged {Count} audit log entries older than {Date}", count, request.OlderThan);
            }
            else
            {
                _logger.LogInformation("Dry run: Would purge {Count} audit log entries older than {Date}", count, request.OlderThan);
            }

            return Task.FromResult(Result.Success(count));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to purge audit logs");
            return Task.FromResult(Result.Failure<int>(AuditLogErrors.PurgeFailed));
        }
    }

    #region Private Methods

    private static byte[] ExportToJson(List<AuditLogEntry> entries, AuditExportRequest request)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        object data = request.IncludeOldNewValues
            ? entries
            : entries.Select(e => new
            {
                e.Id,
                Action = e.Action.ToString(),
                Category = e.Category.ToString(),
                e.UserId,
                e.Username,
                e.WorkspaceId,
                e.TeamId,
                e.ResourceType,
                e.ResourceId,
                e.Description,
                e.IpAddress,
                e.Success,
                e.ErrorMessage,
                Severity = e.Severity.ToString(),
                e.CorrelationId,
                e.Timestamp,
                Metadata = request.IncludeMetadata ? e.Metadata : null
            });

        return JsonSerializer.SerializeToUtf8Bytes(data, options);
    }

    private static byte[] ExportToCsv(List<AuditLogEntry> entries, AuditExportRequest request)
    {
        var sb = new StringBuilder();

        // Header
        var columns = request.Columns ?? new List<string>
        {
            "Id", "Action", "Category", "UserId", "Username", "WorkspaceId",
            "ResourceType", "ResourceId", "Description", "IpAddress",
            "Success", "ErrorMessage", "Severity", "CorrelationId", "Timestamp"
        };

        sb.AppendLine(string.Join(",", columns));

        // Data rows
        foreach (var entry in entries)
        {
            var values = new List<string>();
            foreach (var col in columns)
            {
                var value = col switch
                {
                    "Id" => entry.Id.ToString(),
                    "Action" => entry.Action.ToString(),
                    "Category" => entry.Category.ToString(),
                    "UserId" => entry.UserId?.ToString() ?? "",
                    "Username" => EscapeCsvValue(entry.Username ?? ""),
                    "WorkspaceId" => entry.WorkspaceId?.ToString() ?? "",
                    "TeamId" => entry.TeamId?.ToString() ?? "",
                    "ResourceType" => EscapeCsvValue(entry.ResourceType ?? ""),
                    "ResourceId" => EscapeCsvValue(entry.ResourceId ?? ""),
                    "Description" => EscapeCsvValue(entry.Description ?? ""),
                    "IpAddress" => entry.IpAddress ?? "",
                    "UserAgent" => EscapeCsvValue(entry.UserAgent ?? ""),
                    "Success" => entry.Success.ToString(),
                    "ErrorMessage" => EscapeCsvValue(entry.ErrorMessage ?? ""),
                    "Severity" => entry.Severity.ToString(),
                    "CorrelationId" => entry.CorrelationId ?? "",
                    "Timestamp" => entry.Timestamp.ToString("O"),
                    _ => ""
                };
                values.Add(value);
            }
            sb.AppendLine(string.Join(",", values));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string EscapeCsvValue(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }

    #endregion
}
