using Aegis.Domain.Entities;
using Aegis.Domain.Repositories;
using Carter;
using Microsoft.AspNetCore.Mvc;

namespace Aegis.Api.Features.Feedback;

public class FeedbackModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/feedback")
            .WithTags("Feedback")
            .RequireAuthorization();

        group.MapPost("/", SubmitFeedback)
            .WithName("SubmitFeedback")
            .WithSummary("Submit feedback for a query");

        group.MapPut("/{id}", UpdateFeedback)
            .WithName("UpdateFeedback")
            .WithSummary("Update existing feedback");

        group.MapGet("/workspace/{workspaceId}", GetWorkspaceFeedback)
            .WithName("GetWorkspaceFeedback")
            .WithSummary("Get feedback for a workspace");

        group.MapGet("/stats/{workspaceId}", GetFeedbackStats)
            .WithName("GetFeedbackStats")
            .WithSummary("Get feedback statistics for a workspace");

        group.MapGet("/{id}", GetFeedbackById)
            .WithName("GetFeedbackById")
            .WithSummary("Get feedback by ID");

        group.MapDelete("/{id}", DeleteFeedback)
            .WithName("DeleteFeedback")
            .WithSummary("Delete feedback");
    }

    private static async Task<IResult> SubmitFeedback(
        [FromBody] SubmitFeedbackRequest request,
        [FromServices] IFeedbackRepository feedbackRepository,
        CancellationToken cancellationToken)
    {
        if (request.Type < 1 || request.Type > 3)
        {
            return Results.BadRequest(new { error = "Invalid feedback type. Must be 1 (Positive), 2 (Negative), or 3 (Neutral)" });
        }

        var feedback = Domain.Entities.Feedback.Create(
            request.QueryHistoryId,
            request.UserId,
            request.WorkspaceId,
            (FeedbackType)request.Type,
            request.Comment);

        await feedbackRepository.AddAsync(feedback, cancellationToken);

        return Results.Created($"/api/feedback/{feedback.Id}", new
        {
            feedback.Id,
            feedback.QueryHistoryId,
            feedback.UserId,
            feedback.WorkspaceId,
            Type = feedback.Type.ToString(),
            feedback.Comment,
            feedback.CreatedAt
        });
    }

    private static async Task<IResult> UpdateFeedback(
        Guid id,
        [FromBody] UpdateFeedbackRequest request,
        [FromServices] IFeedbackRepository feedbackRepository,
        CancellationToken cancellationToken)
    {
        var feedback = await feedbackRepository.GetByIdAsync(id, cancellationToken);
        if (feedback == null)
        {
            return Results.NotFound(new { error = "Feedback not found" });
        }

        if (request.Type < 1 || request.Type > 3)
        {
            return Results.BadRequest(new { error = "Invalid feedback type. Must be 1 (Positive), 2 (Negative), or 3 (Neutral)" });
        }

        feedback.Update((FeedbackType)request.Type, request.Comment);
        await feedbackRepository.UpdateAsync(feedback, cancellationToken);

        return Results.Ok(new
        {
            feedback.Id,
            feedback.QueryHistoryId,
            feedback.UserId,
            feedback.WorkspaceId,
            Type = feedback.Type.ToString(),
            feedback.Comment,
            feedback.UpdatedAt
        });
    }

    private static async Task<IResult> GetWorkspaceFeedback(
        Guid workspaceId,
        [FromQuery] int limit,
        [FromServices] IFeedbackRepository feedbackRepository,
        CancellationToken cancellationToken)
    {
        var feedback = await feedbackRepository.GetByWorkspaceIdAsync(
            workspaceId,
            limit > 0 ? limit : 100,
            cancellationToken);

        return Results.Ok(feedback.Select(f => new
        {
            f.Id,
            f.QueryHistoryId,
            f.UserId,
            f.WorkspaceId,
            Type = f.Type.ToString(),
            f.Comment,
            f.CreatedAt,
            f.UpdatedAt
        }));
    }

    private static async Task<IResult> GetFeedbackStats(
        Guid workspaceId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromServices] IFeedbackRepository feedbackRepository,
        CancellationToken cancellationToken)
    {
        var stats = await feedbackRepository.GetStatsAsync(workspaceId, from, to, cancellationToken);

        return Results.Ok(new
        {
            stats.TotalFeedback,
            stats.PositiveFeedback,
            stats.NegativeFeedback,
            stats.NeutralFeedback,
            stats.PositiveRate,
            stats.TotalWithComments,
            From = from,
            To = to
        });
    }

    private static async Task<IResult> GetFeedbackById(
        Guid id,
        [FromServices] IFeedbackRepository feedbackRepository,
        CancellationToken cancellationToken)
    {
        var feedback = await feedbackRepository.GetByIdAsync(id, cancellationToken);
        if (feedback == null)
        {
            return Results.NotFound(new { error = "Feedback not found" });
        }

        return Results.Ok(new
        {
            feedback.Id,
            feedback.QueryHistoryId,
            feedback.UserId,
            feedback.WorkspaceId,
            Type = feedback.Type.ToString(),
            feedback.Comment,
            feedback.Metadata,
            feedback.CreatedAt,
            feedback.UpdatedAt
        });
    }

    private static async Task<IResult> DeleteFeedback(
        Guid id,
        [FromServices] IFeedbackRepository feedbackRepository,
        CancellationToken cancellationToken)
    {
        var feedback = await feedbackRepository.GetByIdAsync(id, cancellationToken);
        if (feedback == null)
        {
            return Results.NotFound(new { error = "Feedback not found" });
        }

        await feedbackRepository.DeleteAsync(id, cancellationToken);
        return Results.NoContent();
    }
}

public record SubmitFeedbackRequest(
    Guid QueryHistoryId,
    Guid UserId,
    Guid WorkspaceId,
    int Type,
    string? Comment);

public record UpdateFeedbackRequest(
    int Type,
    string? Comment);
