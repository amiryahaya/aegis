using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Service for ingesting entities from documents into the knowledge graph
/// </summary>
public interface IEntityIngestionService
{
    /// <summary>
    /// Ingests entities from text into the knowledge graph
    /// </summary>
    /// <param name="text">The text to extract entities from</param>
    /// <param name="documentId">The source document ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the number of entities ingested</returns>
    Task<Result<EntityIngestionResult>> IngestFromTextAsync(
        string text,
        string documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ingests a specific entity into the knowledge graph
    /// </summary>
    /// <param name="entity">The entity to ingest</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the ingested entity</returns>
    Task<Result<GraphEntity>> IngestEntityAsync(
        GraphEntity entity,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of entity ingestion operation
/// </summary>
public class EntityIngestionResult
{
    /// <summary>
    /// Number of entities successfully ingested
    /// </summary>
    public int EntitiesIngested { get; init; }

    /// <summary>
    /// Number of relationships created
    /// </summary>
    public int RelationshipsCreated { get; init; }

    /// <summary>
    /// IDs of the ingested entities
    /// </summary>
    public required IReadOnlyList<string> EntityIds { get; init; }

    /// <summary>
    /// Any warnings encountered during ingestion
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}
