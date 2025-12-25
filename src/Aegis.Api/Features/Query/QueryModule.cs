using System.Security.Claims;
using Carter;
using MediatR;

namespace Aegis.Api.Features.Query;

public class QueryModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/teams/{teamId:guid}/query")
            .WithTags("Query")
            .RequireAuthorization();

        group.MapPost("/", ExecuteQuery)
            .WithSummary("Query documents using RAG");

        static async Task<IResult> ExecuteQuery(
            Guid teamId,
            QueryRequest request,
            IMediator mediator,
            HttpContext context,
            CancellationToken cancellationToken)
        {
            // Get user ID from claims
            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Results.Unauthorized();
            }

            // Create query command
            var command = new QueryDocument.Command
            {
                TeamId = request.TeamId,
                Query = request.Query,
                MaxResults = request.MaxResults ?? 5,
                ScoreThreshold = request.ScoreThreshold
            };

            var result = await mediator.Send(command, cancellationToken);

            if (result.IsSuccess)
            {
                return Results.Ok(result.Value);
            }

            var error = result.Error!;
            var statusCode = error.Code.Contains("NotFound") ? 404
                : error.Code.Contains("Validation") ? 400
                : error.Code.Contains("Forbidden") ? 403
                : 500;

            return Results.Problem(
                statusCode: statusCode,
                title: error.Code,
                detail: error.Message);
        }
    }
}

public record QueryRequest(
    string Query,
    Guid TeamId,
    int? MaxResults = 5,
    float? ScoreThreshold = null);
