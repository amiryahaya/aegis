using System.Security.Cryptography;
using System.Text;
using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Graph;

public class RelationshipExtractionService : IRelationshipExtractionService
{
    private readonly IGraphService _graphService;
    private readonly ILogger<RelationshipExtractionService> _logger;

    // Maximum distance between entities to consider for co-occurrence (in characters)
    private const int MaxCoOccurrenceDistance = 500;

    // Minimum confidence threshold for creating relationships
    private const double MinConfidenceThreshold = 0.3;

    public RelationshipExtractionService(
        IGraphService graphService,
        ILogger<RelationshipExtractionService> logger)
    {
        _graphService = graphService;
        _logger = logger;
    }

    public async Task<Result<RelationshipExtractionResult>> ExtractRelationshipsAsync(
        string text,
        IReadOnlyList<NamedEntity> entities,
        CancellationToken cancellationToken = default)
    {
        if (text == null)
        {
            return Result<RelationshipExtractionResult>.Failure(
                Error.Validation("RelationshipExtraction.NullText", "Text cannot be null"));
        }

        if (entities == null || entities.Count < 2)
        {
            // No relationships possible with less than 2 entities
            return Result<RelationshipExtractionResult>.Success(new RelationshipExtractionResult
            {
                RelationshipsCreated = 0,
                Relationships = Array.Empty<ExtractedRelationship>()
            });
        }

        try
        {
            var extractedRelationships = new List<ExtractedRelationship>();
            var createdCount = 0;

            // Analyze all entity pairs for co-occurrence
            for (int i = 0; i < entities.Count; i++)
            {
                for (int j = i + 1; j < entities.Count; j++)
                {
                    var entity1 = entities[i];
                    var entity2 = entities[j];

                    // Calculate distance and confidence
                    var distance = Math.Abs(entity1.StartPosition - entity2.StartPosition);

                    if (distance > MaxCoOccurrenceDistance)
                        continue; // Too far apart

                    var confidence = CalculateRelationshipConfidence(entity1, entity2, distance);

                    if (confidence < MinConfidenceThreshold)
                        continue; // Confidence too low

                    // Determine relationship type
                    var relationshipType = DetermineRelationshipType(entity1, entity2);

                    // Generate entity IDs (same logic as EntityIngestionService)
                    var fromEntityId = GenerateEntityId(entity1.Text, entity1.Type.ToString());
                    var toEntityId = GenerateEntityId(entity2.Text, entity2.Type.ToString());

                    // Create the relationship
                    var properties = new Dictionary<string, object>
                    {
                        ["confidence"] = confidence,
                        ["distance"] = distance,
                        ["extractedAt"] = DateTime.UtcNow.ToString("O")
                    };

                    var result = await CreateRelationshipAsync(
                        fromEntityId,
                        toEntityId,
                        relationshipType,
                        confidence,
                        properties,
                        cancellationToken);

                    if (result.IsSuccess)
                    {
                        extractedRelationships.Add(new ExtractedRelationship
                        {
                            FromEntityId = fromEntityId,
                            ToEntityId = toEntityId,
                            RelationshipType = relationshipType,
                            Confidence = confidence,
                            Distance = distance
                        });
                        createdCount++;
                    }
                }
            }

            _logger.LogInformation("Extracted {Count} relationships from {EntityCount} entities",
                createdCount, entities.Count);

            return Result<RelationshipExtractionResult>.Success(new RelationshipExtractionResult
            {
                RelationshipsCreated = createdCount,
                Relationships = extractedRelationships.AsReadOnly()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract relationships");
            return Result<RelationshipExtractionResult>.Failure(
                Error.Internal("RelationshipExtraction.Failed", $"Failed to extract relationships: {ex.Message}"));
        }
    }

    public async Task<Result> CreateRelationshipAsync(
        string fromEntityId,
        string toEntityId,
        string relationshipType,
        double confidence,
        Dictionary<string, object>? properties = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var props = properties ?? new Dictionary<string, object>();
            if (!props.ContainsKey("confidence"))
            {
                props["confidence"] = confidence;
            }

            var result = await _graphService.CreateRelationshipAsync(
                fromEntityId,
                toEntityId,
                relationshipType,
                props,
                cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create relationship from {From} to {To}",
                fromEntityId, toEntityId);
            return Result.Failure(
                Error.Internal("RelationshipExtraction.CreateFailed", $"Failed to create relationship: {ex.Message}"));
        }
    }

    private double CalculateRelationshipConfidence(NamedEntity entity1, NamedEntity entity2, int distance)
    {
        // Base confidence is average of entity confidences
        var baseConfidence = (entity1.Confidence + entity2.Confidence) / 2.0;

        // Proximity factor: closer entities = higher confidence
        // Normalize distance to 0-1 range (0 = very close, 1 = max distance)
        var normalizedDistance = Math.Min(1.0, (double)distance / MaxCoOccurrenceDistance);
        var proximityFactor = 1.0 - normalizedDistance;

        // Combine base confidence with proximity
        var finalConfidence = baseConfidence * (0.5 + 0.5 * proximityFactor);

        return Math.Max(0.0, Math.Min(1.0, finalConfidence));
    }

    private string DetermineRelationshipType(NamedEntity entity1, NamedEntity entity2)
    {
        // Apply domain-specific rules for threat intelligence

        // If one entity looks like a threat actor/organization and another is malware
        if ((IsActorType(entity1.Type) && IsMalwareType(entity2.Type)) ||
            (IsMalwareType(entity1.Type) && IsActorType(entity2.Type)))
        {
            return GraphRelationshipTypes.USES;
        }

        // If one is malware and another is infrastructure (IP/URL)
        if ((IsMalwareType(entity1.Type) && IsInfrastructureType(entity2.Type)) ||
            (IsInfrastructureType(entity1.Type) && IsMalwareType(entity2.Type)))
        {
            return GraphRelationshipTypes.COMMUNICATES_WITH;
        }

        // If both are malware
        if (IsMalwareType(entity1.Type) && IsMalwareType(entity2.Type))
        {
            return GraphRelationshipTypes.RELATED_TO;
        }

        // If one is person/org and another is location
        if ((IsActorType(entity1.Type) && entity2.Type == NEREntityType.Location) ||
            (entity1.Type == NEREntityType.Location && IsActorType(entity2.Type)))
        {
            return GraphRelationshipTypes.ORIGINATES_FROM;
        }

        // Default: generic co-occurrence relationship
        return GraphRelationshipTypes.CO_OCCURS_WITH;
    }

    private bool IsActorType(NEREntityType type)
    {
        return type == NEREntityType.Person || type == NEREntityType.Organization;
    }

    private bool IsMalwareType(NEREntityType type)
    {
        // In NER, malware is typically classified as "Other" with specific patterns
        return type == NEREntityType.Other;
    }

    private bool IsInfrastructureType(NEREntityType type)
    {
        return type == NEREntityType.Url || type == NEREntityType.IpAddress;
    }

    private string GenerateEntityId(string text, string type)
    {
        // Same logic as EntityIngestionService to ensure consistent IDs
        var input = $"{type}:{text.ToLowerInvariant()}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var hashString = Convert.ToHexString(hashBytes).ToLowerInvariant();
        return $"entity-{hashString[..16]}";
    }
}
