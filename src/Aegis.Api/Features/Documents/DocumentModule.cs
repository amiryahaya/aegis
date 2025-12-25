using Aegis.Api.Extensions;
using System.Security.Claims;
using Carter;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Aegis.Api.Features.Documents;

public class DocumentModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/teams/{teamId:guid}/documents")
            .WithTags("Documents")
            .RequireAuthorization();

        group.MapPost("/upload", UploadDocument)
            .WithSummary("Upload a document")
            .DisableAntiforgery(); // Required for file uploads

        static async Task<IResult> UploadDocument(
            Guid teamId,
            [FromForm] Guid dataSourceId,
            [FromForm] IFormFile file,
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

            if (file.Length == 0)
            {
                return Results.BadRequest(new { error = "File is empty" });
            }

            // Create upload command
            var command = new UploadDocument.Command
            {
                TeamId = teamId,
                DataSourceId = dataSourceId,
                UserId = userId,
                FileName = file.FileName,
                FileStream = file.OpenReadStream(),
                ContentType = file.ContentType,
                FileSize = file.Length
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
                : error.Code.Contains("Conflict") ? 409
                : 500;

            return Results.Problem(
                statusCode: statusCode,
                title: error.Code,
                detail: error.Message);
        }
    }
}
