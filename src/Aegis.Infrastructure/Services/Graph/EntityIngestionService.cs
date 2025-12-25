using System.Security.Cryptography;
using System.Text;
using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Graph;

public class EntityIngestionService : IEntityIngestionService
{
    private readonly IGraphService _graphService;
    private readonly INERService _nerService;
    private readonly ILogger<EntityIngestionService> _logger;

    public EntityIngestionService(
        IGraphService graphService,
        INERService nerService,
        ILogger<EntityIngestionService> logger)
    {
        _graphService = graphService;
        _nerService = nerService;
        _logger = logger;
    }

    public async Task<Result<EntityIngestionResult>> IngestFromTextAsync(
        string text,
        string documentId,
        CancellationToken cancellationToken = default)
    {
        if (text == null)
        {
            return Result<EntityIngestionResult>.Failure(
                Error.Validation("EntityIngestion.NullText", "Text cannot be null"));
        }

        try
        {
            // Extract entities using NER service
            var nerResult = await _nerService.ExtractEntitiesAsync(text, cancellationToken);
            if (nerResult.IsFailure)
            {
                return Result<EntityIngestionResult>.Failure(nerResult.Error!);
            }

            var namedEntities = nerResult.Value;
            var ingestedIds = new List<string>();
            var relationshipsCreated = 0;

            // Convert NER entities to graph entities and ingest
            foreach (var namedEntity in namedEntities)
            {
                var graphEntity = MapToGraphEntity(namedEntity, documentId);
                var ingestResult = await IngestEntityAsync(graphEntity, cancellationToken);

                if (ingestResult.IsSuccess)
                {
                    ingestedIds.Add(ingestResult.Value.Id);
                }
                else
                {
                    _logger.LogWarning("Failed to ingest entity {Text}: {Error}",
                        namedEntity.Text, ingestResult.Error?.Message);
                }
            }

            // Create MENTIONS relationships from document
            foreach (var entityId in ingestedIds)
            {
                var relResult = await _graphService.CreateRelationshipAsync(
                    documentId,
                    entityId,
                    GraphRelationshipTypes.MENTIONS,
                    new Dictionary<string, object>
                    {
                        ["timestamp"] = DateTime.UtcNow.ToString("O")
                    },
                    cancellationToken);

                if (relResult.IsSuccess)
                {
                    relationshipsCreated++;
                }
            }

            return Result<EntityIngestionResult>.Success(new EntityIngestionResult
            {
                EntitiesIngested = ingestedIds.Count,
                RelationshipsCreated = relationshipsCreated,
                EntityIds = ingestedIds.AsReadOnly()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ingest entities from text");
            return Result<EntityIngestionResult>.Failure(
                Error.Internal("EntityIngestion.Failed", $"Failed to ingest entities: {ex.Message}"));
        }
    }

    public async Task<Result<GraphEntity>> IngestEntityAsync(
        GraphEntity entity,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Use graph service to upsert the entity
            var result = await _graphService.UpsertEntityAsync(entity, cancellationToken);

            if (result.IsSuccess)
            {
                _logger.LogDebug("Ingested entity {EntityId} of type {EntityType}",
                    entity.Id, entity.Type);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ingest entity {EntityId}", entity.Id);
            return Result<GraphEntity>.Failure(
                Error.Internal("EntityIngestion.EntityFailed", $"Failed to ingest entity: {ex.Message}"));
        }
    }

    private GraphEntity MapToGraphEntity(NamedEntity namedEntity, string sourceDocumentId)
    {
        // Generate a deterministic ID based on entity text and type
        var entityId = GenerateEntityId(namedEntity.Text, namedEntity.Type.ToString());

        // Map NER entity type to graph entity type
        var graphEntityType = MapNERTypeToGraphType(namedEntity.Type);

        return new GraphEntity
        {
            Id = entityId,
            Type = graphEntityType,
            Name = namedEntity.Text,
            Properties = new Dictionary<string, object>
            {
                ["sourceDocument"] = sourceDocumentId,
                ["extractedPosition"] = namedEntity.StartPosition,
                ["nerType"] = namedEntity.Type.ToString()
            },
            Confidence = namedEntity.Confidence,
            FirstSeen = DateTime.UtcNow,
            LastSeen = DateTime.UtcNow
        };
    }

    private string GenerateEntityId(string text, string type)
    {
        // Create a deterministic ID using SHA256 hash
        var input = $"{type}:{text.ToLowerInvariant()}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var hashString = Convert.ToHexString(hashBytes).ToLowerInvariant();
        return $"entity-{hashString[..16]}"; // Use first 16 chars
    }

    private EntityType MapNERTypeToGraphType(NEREntityType nerType)
    {
        return nerType switch
        {
            NEREntityType.Person => EntityType.Person,
            NEREntityType.Organization => EntityType.Organization,
            NEREntityType.Location => EntityType.Location,
            NEREntityType.Email => EntityType.Indicator,
            NEREntityType.Url => EntityType.Infrastructure,
            NEREntityType.IpAddress => EntityType.Infrastructure,
            NEREntityType.PhoneNumber => EntityType.Indicator,
            _ => EntityType.Other
        };
    }
}
