using System.ComponentModel;
using System.Text.Json;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace Aegis.Api.Features.Agents.Plugins;

/// <summary>
/// Semantic Kernel plugin for knowledge graph queries
/// </summary>
public class GraphQueryPlugin
{
    private readonly IGraphService _graphService;
    private readonly ILogger<GraphQueryPlugin> _logger;

    public GraphQueryPlugin(
        IGraphService graphService,
        ILogger<GraphQueryPlugin> logger)
    {
        _graphService = graphService;
        _logger = logger;
    }

    [KernelFunction, Description("Get an entity and its network of relationships from the knowledge graph")]
    public async Task<string> GetEntityNetworkAsync(
        [Description("The entity ID to retrieve")] string entityId,
        [Description("Maximum number of hops/relationships to traverse (default: 2)")] int maxHops = 2,
        CancellationToken cancellationToken = default)
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(entityId))
        {
            return JsonSerializer.Serialize(new { error = "Entity ID cannot be empty" });
        }

        _logger.LogInformation(
            "Get entity network: EntityId='{EntityId}', MaxHops={MaxHops}",
            entityId, maxHops);

        // Get entity network
        var result = await _graphService.GetEntityNetworkAsync(
            entityId,
            maxHops,
            cancellationToken);

        // Handle result
        return result.Match(
            success =>
            {
                var response = new
                {
                    success = true,
                    entity = new
                    {
                        id = success.Entity.Id,
                        name = success.Entity.Name,
                        type = success.Entity.Type.ToString(),
                        properties = success.Entity.Properties,
                        confidence = success.Entity.Confidence
                    },
                    relationshipCount = success.Relationships.Count,
                    relationships = success.Relationships.Select(r => new
                    {
                        type = r.Type,
                        relatedEntity = new
                        {
                            id = r.Entity.Id,
                            name = r.Entity.Name,
                            type = r.Entity.Type.ToString()
                        },
                        confidence = r.Confidence,
                        properties = r.Properties
                    })
                };

                return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
            },
            error =>
            {
                _logger.LogWarning(
                    "Get entity network failed: {ErrorCode} - {ErrorMessage}",
                    error.Code, error.Message);

                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Query failed: {error.Message}"
                });
            });
    }

    [KernelFunction, Description("Find paths between two entities in the knowledge graph")]
    public async Task<string> FindPathAsync(
        [Description("Source entity ID")] string sourceEntityId,
        [Description("Target entity ID")] string targetEntityId,
        [Description("Maximum path length (default: 5)")] int maxDepth = 5,
        CancellationToken cancellationToken = default)
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(sourceEntityId))
        {
            return JsonSerializer.Serialize(new { error = "Source entity ID cannot be empty" });
        }

        if (string.IsNullOrWhiteSpace(targetEntityId))
        {
            return JsonSerializer.Serialize(new { error = "Target entity ID cannot be empty" });
        }

        _logger.LogInformation(
            "Finding path: Source={SourceId}, Target={TargetId}, MaxDepth={MaxDepth}",
            sourceEntityId, targetEntityId, maxDepth);

        // Find paths
        var result = await _graphService.FindPathsAsync(
            sourceEntityId,
            targetEntityId,
            maxDepth,
            cancellationToken);

        // Handle result
        return result.Match(
            success =>
            {
                var paths = success.Select(p => new
                {
                    entities = p.Entities.Select(e => new
                    {
                        id = e.Id,
                        name = e.Name,
                        type = e.Type.ToString()
                    }),
                    relationshipTypes = p.RelationshipTypes,
                    pathLength = p.Length
                }).ToList();

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    pathCount = paths.Count,
                    paths
                }, new JsonSerializerOptions { WriteIndented = true });
            },
            error =>
            {
                _logger.LogWarning(
                    "Path finding failed: {ErrorCode} - {ErrorMessage}",
                    error.Code, error.Message);

                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Path finding failed: {error.Message}"
                });
            });
    }
}
