using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Service for querying the knowledge graph with fluent API
/// </summary>
public interface IGraphQueryService
{
    /// <summary>
    /// Starts a new query builder
    /// </summary>
    IGraphQueryBuilder Query();

    /// <summary>
    /// Finds entities similar to the given entity
    /// </summary>
    /// <param name="entityId">The entity ID to find similar entities for</param>
    /// <param name="limit">Maximum number of results</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of similar entities with similarity scores</returns>
    Task<Result<IReadOnlyList<SimilarEntity>>> FindSimilarEntitiesAsync(
        string entityId,
        int limit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets entities by relationship pattern
    /// </summary>
    /// <param name="sourceEntityId">Source entity ID</param>
    /// <param name="relationshipType">Type of relationship</param>
    /// <param name="limit">Maximum number of results</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of related entities</returns>
    Task<Result<IReadOnlyList<GraphEntity>>> GetRelatedEntitiesAsync(
        string sourceEntityId,
        string relationshipType,
        int limit = 100,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Fluent query builder for graph queries
/// </summary>
public interface IGraphQueryBuilder
{
    /// <summary>
    /// Filters entities by type
    /// </summary>
    IGraphQueryBuilder OfType(EntityType entityType);

    /// <summary>
    /// Filters entities by property value
    /// </summary>
    IGraphQueryBuilder WithProperty(string propertyName, object value);

    /// <summary>
    /// Filters by minimum confidence score
    /// </summary>
    IGraphQueryBuilder WithMinConfidence(double minConfidence);

    /// <summary>
    /// Filters entities created after a date
    /// </summary>
    IGraphQueryBuilder CreatedAfter(DateTime date);

    /// <summary>
    /// Orders results by a property
    /// </summary>
    IGraphQueryBuilder OrderBy(string propertyName, bool descending = false);

    /// <summary>
    /// Limits the number of results
    /// </summary>
    IGraphQueryBuilder Limit(int count);

    /// <summary>
    /// Executes the query and returns entities
    /// </summary>
    Task<Result<IReadOnlyList<GraphEntity>>> ExecuteAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the query and returns count
    /// </summary>
    Task<Result<int>> CountAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an entity with similarity score
/// </summary>
public class SimilarEntity
{
    /// <summary>
    /// The similar entity
    /// </summary>
    public required GraphEntity Entity { get; init; }

    /// <summary>
    /// Similarity score (0.0 to 1.0)
    /// </summary>
    public double SimilarityScore { get; init; }

    /// <summary>
    /// Reason for similarity (shared relationships, properties, etc.)
    /// </summary>
    public string? SimilarityReason { get; init; }
}
