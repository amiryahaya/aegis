using System.ComponentModel;
using System.Text.Json;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace Aegis.Api.Features.Agents.Plugins;

/// <summary>
/// Semantic Kernel plugin for entity resolution and lookup
/// </summary>
public class EntityLookupPlugin
{
    private readonly IGraphService _graphService;
    private readonly ILogger<EntityLookupPlugin> _logger;

    public EntityLookupPlugin(
        IGraphService graphService,
        ILogger<EntityLookupPlugin> logger)
    {
        _graphService = graphService;
        _logger = logger;
    }

    [KernelFunction, Description("Search for entities by type and properties in the knowledge graph")]
    public async Task<string> SearchEntitiesAsync(
        [Description("Entity type to search for (Person, Organization, Location, etc.)")] string entityType,
        [Description("Maximum number of results (default: 20)")] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(entityType))
        {
            return JsonSerializer.Serialize(new { error = "Entity type cannot be empty" });
        }

        if (!Enum.TryParse<EntityType>(entityType, true, out var parsedType))
        {
            return JsonSerializer.Serialize(new { error = $"Invalid entity type: {entityType}" });
        }

        _logger.LogInformation(
            "Searching entities: Type={EntityType}, Limit={Limit}",
            parsedType, limit);

        // Search entities
        var result = await _graphService.SearchEntitiesAsync(
            parsedType,
            properties: null,
            limit,
            cancellationToken);

        // Handle result
        return result.Match(
            success =>
            {
                var entities = success.Select(e => new
                {
                    id = e.Id,
                    name = e.Name,
                    type = e.Type.ToString(),
                    confidence = e.Confidence,
                    firstSeen = e.FirstSeen,
                    lastSeen = e.LastSeen,
                    properties = e.Properties
                }).ToList();

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    count = entities.Count,
                    entities
                }, new JsonSerializerOptions { WriteIndented = true });
            },
            error =>
            {
                _logger.LogWarning(
                    "Entity search failed: {ErrorCode} - {ErrorMessage}",
                    error.Code, error.Message);

                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Search failed: {error.Message}"
                });
            });
    }
}
