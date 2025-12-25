using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Service for managing Neo4j graph schema
/// </summary>
public interface IGraphSchemaService
{
    /// <summary>
    /// Initializes the graph database schema with indexes and constraints
    /// </summary>
    Task<Result> InitializeSchemaAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies that the schema is properly initialized
    /// </summary>
    Task<Result<bool>> VerifySchemaAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Common relationship types for threat intelligence graph
/// </summary>
public static class GraphRelationshipTypes
{
    // Actor relationships
    public const string ATTRIBUTED_TO = "ATTRIBUTED_TO";
    public const string USES = "USES";
    public const string TARGETS = "TARGETS";
    public const string ORIGINATES_FROM = "ORIGINATES_FROM";
    public const string EMPLOYS = "EMPLOYS";

    // Malware relationships
    public const string VARIANT_OF = "VARIANT_OF";
    public const string COMMUNICATES_WITH = "COMMUNICATES_WITH";
    public const string DROPS = "DROPS";
    public const string DOWNLOADS = "DOWNLOADS";

    // Vulnerability relationships
    public const string EXPLOITS = "EXPLOITS";
    public const string MITIGATES = "MITIGATES";
    public const string AFFECTS = "AFFECTS";

    // Campaign relationships
    public const string PART_OF = "PART_OF";
    public const string RELATED_TO = "RELATED_TO";

    // Infrastructure relationships
    public const string HOSTS = "HOSTS";
    public const string RESOLVES_TO = "RESOLVES_TO";
    public const string INDICATES = "INDICATES";

    // Temporal relationships
    public const string FOLLOWED_BY = "FOLLOWED_BY";
    public const string CO_OCCURS_WITH = "CO_OCCURS_WITH";

    // Generic relationships
    public const string SIMILAR_TO = "SIMILAR_TO";
    public const string MENTIONS = "MENTIONS";
}
