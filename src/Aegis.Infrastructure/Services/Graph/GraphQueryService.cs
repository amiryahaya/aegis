using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;

namespace Aegis.Infrastructure.Services.Graph;

public class GraphQueryService : IGraphQueryService
{
    private readonly IDriver _driver;
    private readonly ILogger<GraphQueryService> _logger;

    public GraphQueryService(IDriver driver, ILogger<GraphQueryService> logger)
    {
        _driver = driver;
        _logger = logger;
    }

    public IGraphQueryBuilder Query()
    {
        return new GraphQueryBuilder(_driver, _logger);
    }

    public async Task<Result<IReadOnlyList<SimilarEntity>>> FindSimilarEntitiesAsync(
        string entityId,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var session = _driver.AsyncSession();

            var similarEntities = await session.ExecuteReadAsync(async tx =>
            {
                // Find entities with shared relationships
                var query = @"
                    MATCH (e {id: $entityId})-[r1]-(shared)-[r2]-(similar)
                    WHERE similar.id <> $entityId
                    WITH similar, count(DISTINCT shared) as sharedCount, labels(similar)[0] as type
                    RETURN similar, sharedCount, type
                    ORDER BY sharedCount DESC
                    LIMIT $limit";

                var cursor = await tx.RunAsync(query, new { entityId, limit });
                var results = new List<SimilarEntity>();

                await foreach (var record in cursor)
                {
                    var node = record["similar"].As<INode>();
                    var sharedCount = record["sharedCount"].As<int>();
                    var typeStr = record["type"].As<string>();
                    var entityType = Enum.Parse<EntityType>(typeStr);

                    var entity = MapToGraphEntity(node, entityType);
                    var similarityScore = CalculateSimilarityScore(sharedCount);

                    results.Add(new SimilarEntity
                    {
                        Entity = entity,
                        SimilarityScore = similarityScore,
                        SimilarityReason = $"Shares {sharedCount} common relationships"
                    });
                }

                return results;
            });

            return Result<IReadOnlyList<SimilarEntity>>.Success(similarEntities.AsReadOnly());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find similar entities for {EntityId}", entityId);
            return Result<IReadOnlyList<SimilarEntity>>.Failure(
                Error.Internal("GraphQuery.SimilarityFailed", $"Failed to find similar entities: {ex.Message}"));
        }
    }

    public async Task<Result<IReadOnlyList<GraphEntity>>> GetRelatedEntitiesAsync(
        string sourceEntityId,
        string relationshipType,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var session = _driver.AsyncSession();

            var relatedEntities = await session.ExecuteReadAsync(async tx =>
            {
                var query = $@"
                    MATCH (source {{id: $sourceId}})-[:{relationshipType}]-(related)
                    RETURN related, labels(related)[0] as type
                    LIMIT $limit";

                var cursor = await tx.RunAsync(query, new { sourceId = sourceEntityId, limit });
                var results = new List<GraphEntity>();

                await foreach (var record in cursor)
                {
                    var node = record["related"].As<INode>();
                    var typeStr = record["type"].As<string>();
                    var entityType = Enum.Parse<EntityType>(typeStr);
                    results.Add(MapToGraphEntity(node, entityType));
                }

                return results;
            });

            return Result<IReadOnlyList<GraphEntity>>.Success(relatedEntities.AsReadOnly());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get related entities for {EntityId}", sourceEntityId);
            return Result<IReadOnlyList<GraphEntity>>.Failure(
                Error.Internal("GraphQuery.RelatedFailed", $"Failed to get related entities: {ex.Message}"));
        }
    }

    private double CalculateSimilarityScore(int sharedCount)
    {
        // Logarithmic scaling for similarity score
        // 1 shared = ~0.5, 5 shared = ~0.7, 10 shared = ~0.8, 20+ shared = ~0.9+
        return Math.Min(1.0, Math.Log(sharedCount + 1) / Math.Log(25));
    }

    private GraphEntity MapToGraphEntity(INode node, EntityType type)
    {
        var properties = node.Properties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
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

public class GraphQueryBuilder : IGraphQueryBuilder
{
    private readonly IDriver _driver;
    private readonly Microsoft.Extensions.Logging.ILogger _logger;
    private EntityType? _entityType;
    private readonly Dictionary<string, object> _propertyFilters = new();
    private double? _minConfidence;
    private DateTime? _createdAfter;
    private string? _orderByProperty;
    private bool _orderDescending;
    private int? _limit;

    public GraphQueryBuilder(IDriver driver, Microsoft.Extensions.Logging.ILogger logger)
    {
        _driver = driver;
        _logger = logger;
    }

    public IGraphQueryBuilder OfType(EntityType entityType)
    {
        _entityType = entityType;
        return this;
    }

    public IGraphQueryBuilder WithProperty(string propertyName, object value)
    {
        _propertyFilters[propertyName] = value;
        return this;
    }

    public IGraphQueryBuilder WithMinConfidence(double minConfidence)
    {
        _minConfidence = minConfidence;
        return this;
    }

    public IGraphQueryBuilder CreatedAfter(DateTime date)
    {
        _createdAfter = date;
        return this;
    }

    public IGraphQueryBuilder OrderBy(string propertyName, bool descending = false)
    {
        _orderByProperty = propertyName;
        _orderDescending = descending;
        return this;
    }

    public IGraphQueryBuilder Limit(int count)
    {
        _limit = count;
        return this;
    }

    public async Task<Result<IReadOnlyList<GraphEntity>>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var session = _driver.AsyncSession();

            var entities = await session.ExecuteReadAsync(async tx =>
            {
                var (query, parameters) = BuildQuery(forCount: false);
                var cursor = await tx.RunAsync(query, parameters);
                var results = new List<GraphEntity>();

                await foreach (var record in cursor)
                {
                    var node = record["e"].As<INode>();
                    var typeStr = record["type"].As<string>();
                    var type = Enum.Parse<EntityType>(typeStr);

                    var properties = node.Properties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                    properties.Remove("id");
                    properties.Remove("name");
                    properties.Remove("confidence");
                    properties.Remove("firstSeen");
                    properties.Remove("lastSeen");

                    results.Add(new GraphEntity
                    {
                        Id = node.Properties["id"].As<string>(),
                        Type = type,
                        Name = node.Properties["name"].As<string>(),
                        Properties = properties,
                        FirstSeen = DateTime.Parse(node.Properties.GetValueOrDefault("firstSeen", DateTime.UtcNow.ToString("O")).As<string>()),
                        LastSeen = DateTime.Parse(node.Properties.GetValueOrDefault("lastSeen", DateTime.UtcNow.ToString("O")).As<string>()),
                        Confidence = node.Properties.GetValueOrDefault("confidence", 1.0).As<double>()
                    });
                }

                return results;
            });

            return Result<IReadOnlyList<GraphEntity>>.Success(entities.AsReadOnly());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute graph query");
            return Result<IReadOnlyList<GraphEntity>>.Failure(
                Error.Internal("GraphQuery.ExecuteFailed", $"Failed to execute query: {ex.Message}"));
        }
    }

    public async Task<Result<int>> CountAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var session = _driver.AsyncSession();

            var count = await session.ExecuteReadAsync(async tx =>
            {
                var (query, parameters) = BuildQuery(forCount: true);
                var cursor = await tx.RunAsync(query, parameters);
                var record = await cursor.SingleAsync();
                return record["count"].As<int>();
            });

            return Result<int>.Success(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute count query");
            return Result<int>.Failure(
                Error.Internal("GraphQuery.CountFailed", $"Failed to execute count: {ex.Message}"));
        }
    }

    private (string query, object parameters) BuildQuery(bool forCount)
    {
        var typeLabel = _entityType.HasValue ? $":{_entityType.Value}" : "";
        var matchClause = $"MATCH (e{typeLabel})";

        var whereClauses = new List<string>();
        var parameters = new Dictionary<string, object>();

        // Add property filters
        foreach (var (key, value) in _propertyFilters)
        {
            whereClauses.Add($"e.{key} = ${key}");
            parameters[key] = value;
        }

        // Add confidence filter
        if (_minConfidence.HasValue)
        {
            whereClauses.Add("e.confidence >= $minConfidence");
            parameters["minConfidence"] = _minConfidence.Value;
        }

        // Add date filter
        if (_createdAfter.HasValue)
        {
            whereClauses.Add("e.firstSeen >= $createdAfter");
            parameters["createdAfter"] = _createdAfter.Value.ToString("O");
        }

        var whereClause = whereClauses.Any() ? " WHERE " + string.Join(" AND ", whereClauses) : "";

        string query;
        if (forCount)
        {
            query = $"{matchClause}{whereClause} RETURN count(e) as count";
        }
        else
        {
            var returnClause = "RETURN e, labels(e)[0] as type";

            var orderClause = "";
            if (!string.IsNullOrEmpty(_orderByProperty))
            {
                var direction = _orderDescending ? "DESC" : "ASC";
                orderClause = $" ORDER BY e.{_orderByProperty} {direction}";
            }

            var limitClause = _limit.HasValue ? $" LIMIT {_limit.Value}" : "";

            query = $"{matchClause}{whereClause} {returnClause}{orderClause}{limitClause}";
        }

        return (query, parameters);
    }
}
