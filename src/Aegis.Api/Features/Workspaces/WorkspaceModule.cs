using Aegis.Domain.Entities;
using Aegis.Domain.Repositories;
using Carter;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Aegis.Api.Features.Workspaces;

public class WorkspaceModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/workspaces")
            .WithTags("Workspaces");

        group.MapPost("/", CreateWorkspace);
        group.MapGet("/", GetAllWorkspaces);
        group.MapGet("/{id:guid}", GetWorkspace);
        group.MapPut("/{id:guid}", UpdateWorkspace);
        group.MapDelete("/{id:guid}", DeleteWorkspace);
        group.MapPost("/{id:guid}/archive", ArchiveWorkspace);
    }

    private static async Task<Results<Created<WorkspaceResponse>, BadRequest<ProblemDetails>>> CreateWorkspace(
        CreateWorkspaceRequest request,
        IWorkspaceRepository repository,
        IUserRepository userRepository,
        ITeamRepository teamRepository)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Invalid workspace name",
                Detail = "Workspace name is required."
            });
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

        // Verify team exists if provided
        if (request.TeamId.HasValue && !await teamRepository.ExistsAsync(request.TeamId.Value))
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Invalid team",
                Detail = "The specified team does not exist."
            });
        }

        var workspace = Workspace.Create(request.Name, request.CreatedBy, request.Description, request.TeamId);
        await repository.AddAsync(workspace);

        var response = ToResponse(workspace);
        return TypedResults.Created($"/api/workspaces/{workspace.Id}", response);
    }

    private static async Task<Ok<List<WorkspaceResponse>>> GetAllWorkspaces(IWorkspaceRepository repository)
    {
        var workspaces = await repository.GetAllAsync();
        var response = workspaces.Select(ToResponse).ToList();
        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<WorkspaceResponse>, NotFound>> GetWorkspace(
        Guid id,
        IWorkspaceRepository repository)
    {
        var workspace = await repository.GetByIdAsync(id);
        if (workspace is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(ToResponse(workspace));
    }

    private static async Task<Results<Ok<WorkspaceResponse>, NotFound>> UpdateWorkspace(
        Guid id,
        UpdateWorkspaceRequest request,
        IWorkspaceRepository repository)
    {
        var workspace = await repository.GetByIdAsync(id);
        if (workspace is null)
        {
            return TypedResults.NotFound();
        }

        workspace.UpdateDetails(request.Name, request.Description);
        await repository.UpdateAsync(workspace);

        return TypedResults.Ok(ToResponse(workspace));
    }

    private static async Task<Results<NoContent, NotFound>> DeleteWorkspace(
        Guid id,
        IWorkspaceRepository repository)
    {
        if (!await repository.ExistsAsync(id))
        {
            return TypedResults.NotFound();
        }

        await repository.DeleteAsync(id);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<WorkspaceResponse>, NotFound>> ArchiveWorkspace(
        Guid id,
        IWorkspaceRepository repository)
    {
        var workspace = await repository.GetByIdAsync(id);
        if (workspace is null)
        {
            return TypedResults.NotFound();
        }

        workspace.Archive();
        await repository.UpdateAsync(workspace);

        return TypedResults.Ok(ToResponse(workspace));
    }

    private static WorkspaceResponse ToResponse(Workspace workspace) =>
        new(workspace.Id, workspace.Name, workspace.Description, workspace.TeamId, workspace.CreatedBy, workspace.Status.ToString());
}

public record CreateWorkspaceRequest(string Name, Guid CreatedBy, string? Description = null, Guid? TeamId = null);
public record UpdateWorkspaceRequest(string Name, string? Description = null);
public record WorkspaceResponse(Guid Id, string Name, string? Description, Guid? TeamId, Guid CreatedBy, string Status);
