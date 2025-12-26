using Aegis.Domain.Repositories;
using Carter;
using Microsoft.AspNetCore.Mvc;

namespace Aegis.Api.Features.QueryHistory;

public class QueryHistoryModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/query-history")
            .WithTags("Query History")
            .RequireAuthorization();

        group.MapGet("/workspace/{workspaceId}", GetWorkspaceQueryHistory)
            .WithName("GetWorkspaceQueryHistory")
            .WithSummary("Get query history for a workspace");

        group.MapGet("/user/{userId}", GetUserQueryHistory)
            .WithName("GetUserQueryHistory")
            .WithSummary("Get query history for a user");

        group.MapGet("/conversation/{conversationId}", GetConversationQueryHistory)
            .WithName("GetConversationQueryHistory")
            .WithSummary("Get query history for a conversation");

        group.MapGet("/search", SearchQueryHistory)
            .WithName("SearchQueryHistory")
            .WithSummary("Search query history");

        group.MapGet("/stats/{workspaceId}", GetQueryHistoryStats)
            .WithName("GetQueryHistoryStats")
            .WithSummary("Get query history statistics for a workspace");

        group.MapGet("/{id}", GetQueryHistoryById)
            .WithName("GetQueryHistoryById")
            .WithSummary("Get query history by ID");
    }

    private static async Task<IResult> GetWorkspaceQueryHistory(
        Guid workspaceId,
        [FromQuery] int limit,
        [FromServices] IQueryHistoryRepository queryHistoryRepository,
        CancellationToken cancellationToken)
    {
        var history = await queryHistoryRepository.GetByWorkspaceIdAsync(
            workspaceId,
            limit > 0 ? limit : 50,
            cancellationToken);

        return Results.Ok(history.Select(h => new
        {
            h.Id,
            h.WorkspaceId,
            h.UserId,
            h.ConversationId,
            h.Query,
            ResponsePreview = h.Response.Length > 200 ? h.Response.Substring(0, 200) + "..." : h.Response,
            h.TokensUsed,
            ResponseTimeMs = h.ResponseTime.TotalMilliseconds,
            h.ChunksRetrieved,
            h.RetrievalMethod,
            h.CreatedAt
        }));
    }

    private static async Task<IResult> GetUserQueryHistory(
        Guid userId,
        [FromQuery] int limit,
        [FromServices] IQueryHistoryRepository queryHistoryRepository,
        CancellationToken cancellationToken)
    {
        var history = await queryHistoryRepository.GetByUserIdAsync(
            userId,
            limit > 0 ? limit : 50,
            cancellationToken);

        return Results.Ok(history.Select(h => new
        {
            h.Id,
            h.WorkspaceId,
            h.UserId,
            h.ConversationId,
            h.Query,
            ResponsePreview = h.Response.Length > 200 ? h.Response.Substring(0, 200) + "..." : h.Response,
            h.TokensUsed,
            ResponseTimeMs = h.ResponseTime.TotalMilliseconds,
            h.ChunksRetrieved,
            h.RetrievalMethod,
            h.CreatedAt
        }));
    }

    private static async Task<IResult> GetConversationQueryHistory(
        Guid conversationId,
        [FromServices] IQueryHistoryRepository queryHistoryRepository,
        CancellationToken cancellationToken)
    {
        var history = await queryHistoryRepository.GetByConversationIdAsync(conversationId, cancellationToken);

        return Results.Ok(history.Select(h => new
        {
            h.Id,
            h.WorkspaceId,
            h.UserId,
            h.Query,
            h.Response,
            h.TokensUsed,
            ResponseTimeMs = h.ResponseTime.TotalMilliseconds,
            h.ChunksRetrieved,
            h.RetrievalMethod,
            h.CreatedAt
        }));
    }

    private static async Task<IResult> SearchQueryHistory(
        [FromQuery] Guid workspaceId,
        [FromQuery] string searchTerm,
        [FromQuery] int limit,
        [FromServices] IQueryHistoryRepository queryHistoryRepository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return Results.BadRequest(new { error = "Search term is required" });
        }

        var history = await queryHistoryRepository.SearchAsync(
            workspaceId,
            searchTerm,
            limit > 0 ? limit : 50,
            cancellationToken);

        return Results.Ok(history.Select(h => new
        {
            h.Id,
            h.WorkspaceId,
            h.UserId,
            h.Query,
            ResponsePreview = h.Response.Length > 200 ? h.Response.Substring(0, 200) + "..." : h.Response,
            h.TokensUsed,
            ResponseTimeMs = h.ResponseTime.TotalMilliseconds,
            h.ChunksRetrieved,
            h.RetrievalMethod,
            h.CreatedAt
        }));
    }

    private static async Task<IResult> GetQueryHistoryStats(
        Guid workspaceId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromServices] IQueryHistoryRepository queryHistoryRepository,
        CancellationToken cancellationToken)
    {
        var stats = await queryHistoryRepository.GetStatsAsync(workspaceId, from, to, cancellationToken);

        return Results.Ok(new
        {
            stats.TotalQueries,
            stats.TotalTokensUsed,
            stats.AverageResponseTimeMs,
            stats.AverageChunksRetrieved,
            From = from,
            To = to
        });
    }

    private static async Task<IResult> GetQueryHistoryById(
        Guid id,
        [FromServices] IQueryHistoryRepository queryHistoryRepository,
        CancellationToken cancellationToken)
    {
        var history = await queryHistoryRepository.GetByIdAsync(id, cancellationToken);
        if (history == null)
        {
            return Results.NotFound(new { error = "Query history not found" });
        }

        return Results.Ok(new
        {
            history.Id,
            history.WorkspaceId,
            history.UserId,
            history.ConversationId,
            history.Query,
            history.Response,
            history.TokensUsed,
            ResponseTimeMs = history.ResponseTime.TotalMilliseconds,
            history.ChunksRetrieved,
            history.RetrievalMethod,
            history.Metadata,
            history.CreatedAt
        });
    }
}
