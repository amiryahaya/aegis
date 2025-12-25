using Aegis.Domain.Entities;
using Aegis.Domain.Repositories;
using Carter;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Aegis.Api.Features.Conversations;

public class ConversationModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/workspaces/{workspaceId:guid}/conversations")
            .WithTags("Conversations");

        group.MapPost("/", CreateConversation)
            .WithName("CreateConversation")
            .WithSummary("Create a new conversation in a workspace");

        group.MapGet("/", GetConversationsByWorkspace)
            .WithName("GetConversationsByWorkspace")
            .WithSummary("Get all conversations in a workspace");

        group.MapGet("/{id:guid}", GetConversation)
            .WithName("GetConversation")
            .WithSummary("Get conversation by ID");

        group.MapPut("/{id:guid}", UpdateConversation)
            .WithName("UpdateConversation")
            .WithSummary("Update conversation title");

        group.MapDelete("/{id:guid}", DeleteConversation)
            .WithName("DeleteConversation")
            .WithSummary("Delete a conversation");

        group.MapPost("/{id:guid}/archive", ArchiveConversation)
            .WithName("ArchiveConversation")
            .WithSummary("Archive a conversation");
    }

    private static async Task<Results<Created<ConversationResponse>, BadRequest<ProblemDetails>, NotFound>> CreateConversation(
        Guid workspaceId,
        CreateConversationRequest request,
        IConversationRepository repository,
        IWorkspaceRepository workspaceRepository,
        IUserRepository userRepository)
    {
        // Verify workspace exists
        if (!await workspaceRepository.ExistsAsync(workspaceId))
        {
            return TypedResults.NotFound();
        }

        // Verify creator exists
        if (!await userRepository.ExistsAsync(request.CreatedBy))
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Invalid creator",
                Detail = "The specified creator does not exist."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Invalid conversation title",
                Detail = "Conversation title is required."
            });
        }

        var conversation = Conversation.Create(workspaceId, request.Title, request.CreatedBy);
        await repository.AddAsync(conversation);

        var response = ToResponse(conversation);
        return TypedResults.Created($"/api/workspaces/{workspaceId}/conversations/{conversation.Id}", response);
    }

    private static async Task<Ok<List<ConversationResponse>>> GetConversationsByWorkspace(
        Guid workspaceId,
        IConversationRepository repository)
    {
        var conversations = await repository.GetByWorkspaceIdAsync(workspaceId);
        var response = conversations.Select(ToResponse).ToList();
        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<ConversationResponse>, NotFound>> GetConversation(
        Guid workspaceId,
        Guid id,
        IConversationRepository repository)
    {
        var conversation = await repository.GetByIdAsync(id);
        if (conversation is null || conversation.WorkspaceId != workspaceId)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(ToResponse(conversation));
    }

    private static async Task<Results<Ok<ConversationResponse>, NotFound, BadRequest<ProblemDetails>>> UpdateConversation(
        Guid workspaceId,
        Guid id,
        UpdateConversationRequest request,
        IConversationRepository repository)
    {
        var conversation = await repository.GetByIdAsync(id);
        if (conversation is null || conversation.WorkspaceId != workspaceId)
        {
            return TypedResults.NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Invalid conversation title",
                Detail = "Conversation title is required."
            });
        }

        conversation.UpdateTitle(request.Title);
        await repository.UpdateAsync(conversation);

        return TypedResults.Ok(ToResponse(conversation));
    }

    private static async Task<Results<NoContent, NotFound>> DeleteConversation(
        Guid workspaceId,
        Guid id,
        IConversationRepository repository)
    {
        var conversation = await repository.GetByIdAsync(id);
        if (conversation is null || conversation.WorkspaceId != workspaceId)
        {
            return TypedResults.NotFound();
        }

        await repository.DeleteAsync(id);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<ConversationResponse>, NotFound>> ArchiveConversation(
        Guid workspaceId,
        Guid id,
        IConversationRepository repository)
    {
        var conversation = await repository.GetByIdAsync(id);
        if (conversation is null || conversation.WorkspaceId != workspaceId)
        {
            return TypedResults.NotFound();
        }

        conversation.Archive();
        await repository.UpdateAsync(conversation);

        return TypedResults.Ok(ToResponse(conversation));
    }

    private static ConversationResponse ToResponse(Conversation conversation) =>
        new(
            conversation.Id,
            conversation.WorkspaceId,
            conversation.Title,
            conversation.CreatedBy,
            conversation.Status.ToString(),
            conversation.LastMessageAt,
            conversation.MessageCount,
            conversation.CreatedAt,
            conversation.UpdatedAt);
}

public record CreateConversationRequest(string Title, Guid CreatedBy);
public record UpdateConversationRequest(string Title);
public record ConversationResponse(
    Guid Id,
    Guid WorkspaceId,
    string Title,
    Guid CreatedBy,
    string Status,
    DateTime? LastMessageAt,
    int MessageCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
