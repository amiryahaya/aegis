using Aegis.Domain.Services;
using Carter;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Aegis.Api.Features.Graph;

public class GraphModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/graph")
            .WithTags("Graph");

        group.MapGet("/entities/{id}", GetEntity)
            .WithName("GetEntity")
            .WithSummary("Get entity details by ID");

        group.MapGet("/entities", SearchEntities)
            .WithName("SearchEntities")
            .WithSummary("Search entities with filters");

        group.MapGet("/entities/{id}/network", GetEntityNetwork)
            .WithName("GetEntityNetwork")
            .WithSummary("Get entity network (relationships and connected entities)");

        group.MapGet("/entities/{id}/similar", GetSimilarEntities)
            .WithName("GetSimilarEntities")
            .WithSummary("Find similar entities");

        group.MapGet("/entities/{fromId}/paths/{toId}", FindPaths)
            .WithName("FindPaths")
            .WithSummary("Find paths between two entities");
    }

    private static async Task<Results<Ok<EntityResponse>, NotFound>> GetEntity(
        string id,
        [FromServices] IGraphService graphService)
    {
        // Get entity via network with 0 hops (just the entity itself)
        var result = await graphService.GetEntityNetworkAsync(id, maxHops: 0);

        if (result.IsFailure || result.Value == null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(ToEntityResponse(result.Value.Entity));
    }

    private static async Task<Ok<SearchEntitiesResponse>> SearchEntities(
        [FromQuery] string? type,
        [FromQuery] double? minConfidence,
        [FromQuery] int limit,
        [FromServices] IGraphQueryService queryService)
    {
        var query = queryService.Query();

        if (!string.IsNullOrEmpty(type) && Enum.TryParse<EntityType>(type, true, out var entityType))
        {
            query = query.OfType(entityType);
        }

        if (minConfidence.HasValue)
        {
            query = query.WithMinConfidence(minConfidence.Value);
        }

        query = query.Limit(limit > 0 ? limit : 10);

        var result = await query.ExecuteAsync();

        var entities = result.IsSuccess
            ? result.Value.Select(ToEntityResponse).ToList()
            : new List<EntityResponse>();

        return TypedResults.Ok(new SearchEntitiesResponse(entities, entities.Count));
    }

    private static async Task<Results<Ok<EntityNetworkResponse>, NotFound>> GetEntityNetwork(
        string id,
        [FromQuery] int maxHops,
        [FromServices] IGraphService graphService)
    {
        var result = await graphService.GetEntityNetworkAsync(
            id,
            maxHops: maxHops > 0 ? maxHops : 1);

        if (result.IsFailure || result.Value == null)
        {
            return TypedResults.NotFound();
        }

        var network = result.Value;
        var response = new EntityNetworkResponse(
            ToEntityResponse(network.Entity),
            network.Relationships.Select(r => new RelationshipResponse(
                r.Type,
                ToEntityResponse(r.Entity),
                r.Properties,
                r.Confidence)).ToList());

        return TypedResults.Ok(response);
    }

    private static async Task<Ok<List<SimilarEntityResponse>>> GetSimilarEntities(
        string id,
        [FromQuery] int limit,
        [FromServices] IGraphQueryService queryService)
    {
        var result = await queryService.FindSimilarEntitiesAsync(
            id,
            limit: limit > 0 ? limit : 10);

        var similarEntities = result.IsSuccess
            ? result.Value.Select(s => new SimilarEntityResponse(
                ToEntityResponse(s.Entity),
                s.SimilarityScore)).ToList()
            : new List<SimilarEntityResponse>();

        return TypedResults.Ok(similarEntities);
    }

    private static async Task<Results<Ok<PathsResponse>, NotFound>> FindPaths(
        string fromId,
        string toId,
        [FromQuery] int maxDepth,
        [FromServices] IGraphService graphService)
    {
        var result = await graphService.FindPathsAsync(
            fromId,
            toId,
            maxDepth: maxDepth > 0 ? maxDepth : 3);

        if (result.IsFailure)
        {
            return TypedResults.NotFound();
        }

        var paths = result.Value.Select(p => new PathResponse(
            p.Entities.Select(ToEntityResponse).ToList(),
            p.RelationshipTypes.ToList(),
            p.Length)).ToList();

        return TypedResults.Ok(new PathsResponse(paths));
    }

    private static EntityResponse ToEntityResponse(GraphEntity entity) =>
        new(
            entity.Id,
            entity.Type.ToString(),
            entity.Name,
            entity.Properties,
            entity.Confidence,
            entity.FirstSeen,
            entity.LastSeen);
}

// Response DTOs
public record EntityResponse(
    string Id,
    string Type,
    string Name,
    Dictionary<string, object> Properties,
    double Confidence,
    DateTime? FirstSeen,
    DateTime? LastSeen);

public record SearchEntitiesResponse(
    List<EntityResponse> Entities,
    int TotalCount);

public record RelationshipResponse(
    string Type,
    EntityResponse Entity,
    Dictionary<string, object> Properties,
    double Confidence);

public record EntityNetworkResponse(
    EntityResponse CentralEntity,
    List<RelationshipResponse> Relationships);

public record SimilarEntityResponse(
    EntityResponse Entity,
    double SimilarityScore);

public record PathResponse(
    List<EntityResponse> Entities,
    List<string> RelationshipTypes,
    int Length);

public record PathsResponse(List<PathResponse> Paths);
