using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;

namespace Aegis.Infrastructure.Services.Graph;

public class Neo4jService : IGraphService
{
    private readonly IDriver _driver;
    private readonly ILogger<Neo4jService> _logger;

    public Neo4jService(IDriver driver, ILogger<Neo4jService> logger)
    {
        _driver = driver;
        _logger = logger;
    }

    public async Task<Result<GraphEntity>> UpsertEntityAsync(
        GraphEntity entity,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var session = _driver.AsyncSession();

            var result = await session.ExecuteWriteAsync(async tx =>
            {
                var query = $@"
                    MERGE (e:{entity.Type} {{id: $id}})
                    SET e.name = $name,
                        e.confidence = $confidence,
                        e.firstSeen = COALESCE(e.firstSeen, $firstSeen),
                        e.lastSeen = $lastSeen,
                        e += $properties
                    RETURN e";

                var parameters = new
                {
                    id = entity.Id,
                    name = entity.Name,
                    confidence = entity.Confidence,
                    firstSeen = entity.FirstSeen.ToString("O"),
                    lastSeen = entity.LastSeen.ToString("O"),
                    properties = entity.Properties
                };

                var cursor = await tx.RunAsync(query, parameters);
                var record = await cursor.SingleAsync();
                return MapToGraphEntity(record["e"].As<INode>(), entity.Type);
            });

            return Result<GraphEntity>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert entity: {EntityId}", entity.Id);
            return Result<GraphEntity>.Failure(
                Error.Internal("GraphService.UpsertFailed", $"Failed to upsert entity: {ex.Message}"));
        }
    }

    public async Task<Result> CreateRelationshipAsync(
        string fromEntityId,
        string toEntityId,
        string relationshipType,
        Dictionary<string, object>? properties = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var session = _driver.AsyncSession();

            await session.ExecuteWriteAsync(async tx =>
            {
                var query = $@"
                    MATCH (from {{id: $fromId}})
                    MATCH (to {{id: $toId}})
                    MERGE (from)-[r:{relationshipType}]->(to)
                    SET r += $properties
                    RETURN r";

                var parameters = new
                {
                    fromId = fromEntityId,
                    toId = toEntityId,
                    properties = properties ?? new Dictionary<string, object>()
                };

                await tx.RunAsync(query, parameters);
            });

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create relationship from {From} to {To}", fromEntityId, toEntityId);
            return Result.Failure(
                Error.Internal("GraphService.RelationshipFailed", $"Failed to create relationship: {ex.Message}"));
        }
    }

    public async Task<Result<EntityNetwork>> GetEntityNetworkAsync(
        string entityId,
        int maxHops = 1,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var session = _driver.AsyncSession();

            var network = await session.ExecuteReadAsync(async tx =>
            {
                // Get central entity
                var entityQuery = "MATCH (e {id: $id}) RETURN e, labels(e)[0] as type";
                var entityCursor = await tx.RunAsync(entityQuery, new { id = entityId });
                var entityRecord = await entityCursor.SingleAsync();

                var entityNode = entityRecord["e"].As<INode>();
                var entityTypeStr = entityRecord["type"].As<string>();
                var entityType = Enum.Parse<EntityType>(entityTypeStr);
                var centralEntity = MapToGraphEntity(entityNode, entityType);

                // Get relationships
                var relationshipsQuery = $@"
                    MATCH (e {{id: $id}})-[r]-(related)
                    RETURN type(r) as relType, r, related, labels(related)[0] as relatedType
                    LIMIT 100";

                var relCursor = await tx.RunAsync(relationshipsQuery, new { id = entityId });
                var relationships = new List<EntityRelationship>();

                await foreach (var record in relCursor)
                {
                    var relType = record["relType"].As<string>();
                    var relatedNode = record["related"].As<INode>();
                    var relatedTypeStr = record["relatedType"].As<string>();
                    var relatedType = Enum.Parse<EntityType>(relatedTypeStr);
                    var relatedEntity = MapToGraphEntity(relatedNode, relatedType);

                    var relationship = new EntityRelationship
                    {
                        Type = relType,
                        Entity = relatedEntity,
                        Properties = new Dictionary<string, object>(),
                        Confidence = 1.0
                    };

                    relationships.Add(relationship);
                }

                return new EntityNetwork
                {
                    Entity = centralEntity,
                    Relationships = relationships.AsReadOnly()
                };
            });

            return Result<EntityNetwork>.Success(network);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get entity network for: {EntityId}", entityId);
            return Result<EntityNetwork>.Failure(
                Error.Internal("GraphService.NetworkFailed", $"Failed to get entity network: {ex.Message}"));
        }
    }

    public async Task<Result<IReadOnlyList<GraphEntity>>> SearchEntitiesAsync(
        EntityType? entityType = null,
        Dictionary<string, object>? properties = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var session = _driver.AsyncSession();

            var entities = await session.ExecuteReadAsync(async tx =>
            {
                var typeFilter = entityType.HasValue ? $":{entityType.Value}" : "";
                var query = $@"
                    MATCH (e{typeFilter})
                    RETURN e, labels(e)[0] as type
                    LIMIT $limit";

                var cursor = await tx.RunAsync(query, new { limit });
                var results = new List<GraphEntity>();

                await foreach (var record in cursor)
                {
                    var node = record["e"].As<INode>();
                    var typeStr = record["type"].As<string>();
                    var type = Enum.Parse<EntityType>(typeStr);
                    results.Add(MapToGraphEntity(node, type));
                }

                return results;
            });

            return Result<IReadOnlyList<GraphEntity>>.Success(entities.AsReadOnly());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search entities");
            return Result<IReadOnlyList<GraphEntity>>.Failure(
                Error.Internal("GraphService.SearchFailed", $"Failed to search entities: {ex.Message}"));
        }
    }

    public async Task<Result<IReadOnlyList<EntityPath>>> FindPathsAsync(
        string fromEntityId,
        string toEntityId,
        int maxDepth = 5,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var session = _driver.AsyncSession();

            var paths = await session.ExecuteReadAsync(async tx =>
            {
                var query = $@"
                    MATCH path = shortestPath((from {{id: $fromId}})-[*1..{maxDepth}]-(to {{id: $toId}}))
                    RETURN path
                    LIMIT 10";

                var cursor = await tx.RunAsync(query, new { fromId = fromEntityId, toId = toEntityId });
                var results = new List<EntityPath>();

                await foreach (var record in cursor)
                {
                    var path = record["path"].As<IPath>();
                    var entities = new List<GraphEntity>();
                    var relationshipTypes = new List<string>();

                    foreach (var node in path.Nodes)
                    {
                        var labels = node.Labels;
                        var type = labels.Any() ? Enum.Parse<EntityType>(labels.First()) : EntityType.Other;
                        entities.Add(MapToGraphEntity(node, type));
                    }

                    foreach (var rel in path.Relationships)
                    {
                        relationshipTypes.Add(rel.Type);
                    }

                    results.Add(new EntityPath
                    {
                        Entities = entities.AsReadOnly(),
                        RelationshipTypes = relationshipTypes.AsReadOnly()
                    });
                }

                return results;
            });

            return Result<IReadOnlyList<EntityPath>>.Success(paths.AsReadOnly());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find paths from {From} to {To}", fromEntityId, toEntityId);
            return Result<IReadOnlyList<EntityPath>>.Failure(
                Error.Internal("GraphService.PathFindingFailed", $"Failed to find paths: {ex.Message}"));
        }
    }

    public async Task<Result> DeleteEntityAsync(
        string entityId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var session = _driver.AsyncSession();

            await session.ExecuteWriteAsync(async tx =>
            {
                var query = "MATCH (e {id: $id}) DETACH DELETE e";
                await tx.RunAsync(query, new { id = entityId });
            });

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete entity: {EntityId}", entityId);
            return Result.Failure(
                Error.Internal("GraphService.DeleteFailed", $"Failed to delete entity: {ex.Message}"));
        }
    }

    private static GraphEntity MapToGraphEntity(INode node, EntityType type)
    {
        var properties = node.Properties.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value);

        // Remove standard fields from properties
        properties.Remove("id");
        properties.Remove("name");
        properties.Remove("confidence");
        properties.Remove("firstSeen");
        properties.Remove("lastSeen");

        return new GraphEntity
        {
            Id = node.Properties["id"].As<string>(),
            Type = type,
            Name = node.Properties["name"].As<string>(),
            Properties = properties,
            FirstSeen = DateTime.Parse(node.Properties.GetValueOrDefault("firstSeen", DateTime.UtcNow.ToString("O")).As<string>()),
            LastSeen = DateTime.Parse(node.Properties.GetValueOrDefault("lastSeen", DateTime.UtcNow.ToString("O")).As<string>()),
            Confidence = node.Properties.GetValueOrDefault("confidence", 1.0).As<double>()
        };
    }
}
