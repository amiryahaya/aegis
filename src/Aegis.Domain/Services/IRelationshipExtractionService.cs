using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Service for extracting relationships between entities
/// </summary>
public interface IRelationshipExtractionService
{
    /// <summary>
    /// Extracts relationships from entities found in text
    /// </summary>
    /// <param name="text">The text containing entities</param>
    /// <param name="entities">The entities extracted from the text</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the number of relationships created</returns>
    Task<Result<RelationshipExtractionResult>> ExtractRelationshipsAsync(
        string text,
        IReadOnlyList<NamedEntity> entities,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a relationship between two entities
    /// </summary>
    /// <param name="fromEntityId">Source entity ID</param>
    /// <param name="toEntityId">Target entity ID</param>
    /// <param name="relationshipType">Type of relationship</param>
    /// <param name="confidence">Confidence score (0.0 to 1.0)</param>
    /// <param name="properties">Additional properties</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    Task<Result> CreateRelationshipAsync(
        string fromEntityId,
        string toEntityId,
        string relationshipType,
        double confidence,
        Dictionary<string, object>? properties = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of relationship extraction operation
/// </summary>
public class RelationshipExtractionResult
{
    /// <summary>
    /// Number of relationships successfully created
    /// </summary>
    public int RelationshipsCreated { get; init; }

    /// <summary>
    /// Details of the created relationships
    /// </summary>
    public required IReadOnlyList<ExtractedRelationship> Relationships { get; init; }
}

/// <summary>
/// Represents an extracted relationship between entities
/// </summary>
public class ExtractedRelationship
{
    /// <summary>
    /// Source entity ID
    /// </summary>
    public required string FromEntityId { get; init; }

    /// <summary>
    /// Target entity ID
    /// </summary>
    public required string ToEntityId { get; init; }

    /// <summary>
    /// Type of relationship
    /// </summary>
    public required string RelationshipType { get; init; }

    /// <summary>
    /// Confidence score (0.0 to 1.0)
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// Distance between entities in text (number of characters)
    /// </summary>
    public int Distance { get; init; }
}
