using Aegis.Domain.Services;
using Carter;
using Microsoft.AspNetCore.Mvc;

namespace Aegis.Api.Features.Query;

public class QueryModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/workspaces/{workspaceId:guid}/query")
            .WithTags("Query");

        group.MapPost("/", ExecuteQuery);
        group.MapPost("/stream", ExecuteStreamingQuery);
    }

    private static async Task<IResult> ExecuteQuery(
        Guid workspaceId,
        [FromBody] QueryRequest request,
        [FromQuery] Guid? conversationId,
        IRAGQueryService queryService)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return Results.BadRequest(new { error = "Query cannot be empty" });
        }

        var result = await queryService.QueryAsync(
            request.Query,
            workspaceId,
            conversationId);

        if (result.IsFailure)
        {
            return Results.Problem(
                title: "Query Failed",
                detail: result.Error!.Message,
                statusCode: 500);
        }

        var response = result.Value!;

        return Results.Ok(new
        {
            query = response.Query,
            response = response.Response,
            sources = response.Sources.Select(s => new
            {
                documentId = s.DocumentId,
                documentName = s.DocumentName,
                content = s.Content,
                relevance = s.Relevance,
                chunkIndex = s.ChunkIndex
            }),
            queryAnalysis = new
            {
                intent = response.QueryAnalysis.Intent.ToString(),
                complexity = response.QueryAnalysis.Complexity.ToString(),
                keywords = response.QueryAnalysis.Keywords,
                extractedEntities = response.QueryAnalysis.ExtractedEntities
            },
            tokensUsed = response.TokensUsed,
            processingTime = response.ProcessingTime.TotalMilliseconds,
            conversationId = response.ConversationId
        });
    }

    private static async Task ExecuteStreamingQuery(
        Guid workspaceId,
        [FromBody] QueryRequest request,
        [FromQuery] Guid? conversationId,
        IRAGQueryService queryService,
        HttpContext context)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new { error = "Query cannot be empty" });
            return;
        }

        // Set headers for SSE
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.Append("Cache-Control", "no-cache");
        context.Response.Headers.Append("X-Accel-Buffering", "no");

        await foreach (var chunkResult in queryService.QueryStreamingAsync(
            request.Query,
            workspaceId,
            userId: null, // TODO: Extract from HttpContext.User claims
            conversationId,
            context.RequestAborted))
        {
            if (chunkResult.IsFailure)
            {
                await context.Response.WriteAsync($"data: {{\"error\":\"{chunkResult.Error!.Message}\"}}\n\n");
                break;
            }

            var chunk = chunkResult.Value!;

            if (chunk.IsComplete)
            {
                // Send final metadata
                var metadata = new
                {
                    type = "metadata",
                    sources = chunk.Sources?.Select(s => new
                    {
                        documentId = s.DocumentId,
                        documentName = s.DocumentName,
                        content = s.Content,
                        relevance = s.Relevance,
                        chunkIndex = s.ChunkIndex
                    }),
                    queryAnalysis = chunk.QueryAnalysis != null ? new
                    {
                        intent = chunk.QueryAnalysis.Intent.ToString(),
                        complexity = chunk.QueryAnalysis.Complexity.ToString(),
                        keywords = chunk.QueryAnalysis.Keywords,
                        extractedEntities = chunk.QueryAnalysis.ExtractedEntities
                    } : null
                };

                await context.Response.WriteAsync($"data: {System.Text.Json.JsonSerializer.Serialize(metadata)}\n\n");
                await context.Response.WriteAsync("data: [DONE]\n\n");
            }
            else
            {
                // Send content chunk
                var data = new
                {
                    type = "content",
                    content = chunk.Content
                };

                await context.Response.WriteAsync($"data: {System.Text.Json.JsonSerializer.Serialize(data)}\n\n");
            }

            await context.Response.Body.FlushAsync();
        }
    }
}

public record QueryRequest(string Query);
