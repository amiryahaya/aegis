using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Service for managing and querying the knowledge graph
/// </summary>
public interface IGraphService
{
    /// <summary>
    /// Creates or updates an entity in the graph
    /// </summary>
    Task<Result<GraphEntity>> UpsertEntityAsync(GraphEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a relationship between two entities
    /// </summary>
    Task<Result> CreateRelationshipAsync(string fromEntityId, string toEntityId, string relationshipType, Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an entity by ID with its immediate relationships
    /// </summary>
    Task<Result<EntityNetwork>> GetEntityNetworkAsync(string entityId, int maxHops = 1, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for entities by type and properties
    /// </summary>
    Task<Result<IReadOnlyList<GraphEntity>>> SearchEntitiesAsync(EntityType? entityType = null, Dictionary<string, object>? properties = null, int limit = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds paths between two entities
    /// </summary>
    Task<Result<IReadOnlyList<EntityPath>>> FindPathsAsync(string fromEntityId, string toEntityId, int maxDepth = 5, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an entity and its relationships
    /// </summary>
    Task<Result> DeleteEntityAsync(string entityId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an entity in the knowledge graph
/// </summary>
public class GraphEntity
{
    /// <summary>
    /// Unique identifier for the entity
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Entity type (Person, Organization, Malware, etc.)
    /// </summary>
    public required EntityType Type { get; init; }

    /// <summary>
    /// Entity name/label
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Additional properties
    /// </summary>
    public Dictionary<string, object> Properties { get; init; } = new();

    /// <summary>
    /// When the entity was first seen
    /// </summary>
    public DateTime FirstSeen { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// When the entity was last updated
    /// </summary>
    public DateTime LastSeen { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Confidence score (0.0 to 1.0)
    /// </summary>
    public double Confidence { get; init; } = 1.0;
}

/// <summary>
/// Entity types for the intelligence graph
/// </summary>
public enum EntityType
{
    Person,
    Organization,
    Location,
    Malware,
    ThreatActor,
    Vulnerability,
    Campaign,
    TTP,
    Infrastructure,
    Tool,
    Indicator,
    Other
}

/// <summary>
/// Represents an entity network (entity with its relationships)
/// </summary>
public class EntityNetwork
{
    /// <summary>
    /// Central entity
    /// </summary>
    public required GraphEntity Entity { get; init; }

    /// <summary>
    /// Related entities with their relationships
    /// </summary>
    public IReadOnlyList<EntityRelationship> Relationships { get; init; } = Array.Empty<EntityRelationship>();
}

/// <summary>
/// Represents a relationship between entities
/// </summary>
public class EntityRelationship
{
    /// <summary>
    /// Relationship type (e.g., "USES", "TARGETS", "ATTRIBUTED_TO")
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Related entity
    /// </summary>
    public required GraphEntity Entity { get; init; }

    /// <summary>
    /// Relationship properties
    /// </summary>
    public Dictionary<string, object> Properties { get; init; } = new();

    /// <summary>
    /// Relationship confidence
    /// </summary>
    public double Confidence { get; init; } = 1.0;
}

/// <summary>
/// Represents a path between two entities
/// </summary>
public class EntityPath
{
    /// <summary>
    /// Entities in the path
    /// </summary>
    public required IReadOnlyList<GraphEntity> Entities { get; init; }

    /// <summary>
    /// Relationships in the path
    /// </summary>
    public required IReadOnlyList<string> RelationshipTypes { get; init; }

    /// <summary>
    /// Path length (number of hops)
    /// </summary>
    public int Length => RelationshipTypes.Count;
}
