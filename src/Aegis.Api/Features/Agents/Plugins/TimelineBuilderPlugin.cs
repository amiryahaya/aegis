using System.ComponentModel;
using System.Text.Json;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace Aegis.Api.Features.Agents.Plugins;

/// <summary>
/// Semantic Kernel plugin for building temporal timelines
/// </summary>
public class TimelineBuilderPlugin
{
    private readonly IGraphQueryService _graphQueryService;
    private readonly ILogger<TimelineBuilderPlugin> _logger;

    public TimelineBuilderPlugin(
        IGraphQueryService graphQueryService,
        ILogger<TimelineBuilderPlugin> logger)
    {
        _graphQueryService = graphQueryService;
        _logger = logger;
    }

    [KernelFunction, Description("Build a timeline of events and activities for an entity")]
    public async Task<string> BuildTimelineAsync(
        [Description("Entity ID to build timeline for")] string entityId,
        [Description("Start date in ISO format (optional)")] string? startDate = null,
        [Description("End date in ISO format (optional)")] string? endDate = null,
        CancellationToken cancellationToken = default)
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(entityId))
        {
            return JsonSerializer.Serialize(new { error = "Entity ID cannot be empty" });
        }

        DateTime? parsedStartDate = null;
        DateTime? parsedEndDate = null;

        if (!string.IsNullOrWhiteSpace(startDate) && !DateTime.TryParse(startDate, out var sd))
        {
            return JsonSerializer.Serialize(new { error = "Invalid start date format" });
        }
        else if (!string.IsNullOrWhiteSpace(startDate))
        {
            parsedStartDate = DateTime.Parse(startDate);
        }

        if (!string.IsNullOrWhiteSpace(endDate) && !DateTime.TryParse(endDate, out var ed))
        {
            return JsonSerializer.Serialize(new { error = "Invalid end date format" });
        }
        else if (!string.IsNullOrWhiteSpace(endDate))
        {
            parsedEndDate = DateTime.Parse(endDate);
        }

        _logger.LogInformation(
            "Building timeline: EntityId={EntityId}, StartDate={StartDate}, EndDate={EndDate}",
            entityId, parsedStartDate, parsedEndDate);

        // Query entities created in date range
        var queryBuilder = _graphQueryService.Query();

        if (parsedStartDate.HasValue)
        {
            queryBuilder = queryBuilder.CreatedAfter(parsedStartDate.Value);
        }

        var result = await queryBuilder
            .OrderBy("FirstSeen", descending: true)
            .Limit(100)
            .ExecuteAsync(cancellationToken);

        // Handle result
        return result.Match(
            success =>
            {
                var events = success.Select(e => new
                {
                    timestamp = e.FirstSeen,
                    entityId = e.Id,
                    entityName = e.Name,
                    entityType = e.Type.ToString(),
                    confidence = e.Confidence
                }).ToList();

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    entityId,
                    eventCount = events.Count,
                    timeline = events,
                    dateRange = new
                    {
                        start = parsedStartDate,
                        end = parsedEndDate
                    }
                }, new JsonSerializerOptions { WriteIndented = true });
            },
            error =>
            {
                _logger.LogWarning(
                    "Timeline build failed: {ErrorCode} - {ErrorMessage}",
                    error.Code, error.Message);

                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Timeline build failed: {error.Message}"
                });
            });
    }
}
