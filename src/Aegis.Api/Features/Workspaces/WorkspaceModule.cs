using Aegis.Domain.Entities;
using Aegis.Domain.Repositories;
using Aegis.Domain.Services;
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
        group.MapGet("/{id:guid}/context", GetWorkspaceContext);
        group.MapGet("/{id:guid}/context/relevant", GetRelevantContext);
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

    private static async Task<Results<Ok<WorkspaceContextResponse>, NotFound>> GetWorkspaceContext(
        Guid id,
        IWorkspaceContextService contextService)
    {
        var context = await contextService.GetWorkspaceContextAsync(id);

        return TypedResults.Ok(new WorkspaceContextResponse(
            context.CustomInstructions,
            context.Entities.Select(e => new EntityContextDto(
                e.Id, e.Name, e.Type, e.Description, e.Aliases, e.Confidence)).ToList(),
            context.Findings.Select(f => new FindingContextDto(
                f.Id, f.Title, f.Content, f.Type)).ToList(),
            context.Facts.Select(f => new FactContextDto(
                f.Id, f.Statement, f.Confidence)).ToList(),
            context.FormatAsPromptContext()));
    }

    private static async Task<Results<Ok<WorkspaceContextResponse>, NotFound, BadRequest<string>>> GetRelevantContext(
        Guid id,
        [FromQuery] string? query,
        [FromQuery] int maxEntities,
        [FromQuery] int maxFindings,
        [FromQuery] int maxFacts,
        IWorkspaceContextService contextService)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return TypedResults.BadRequest("Query parameter is required");
        }

        var context = await contextService.GetRelevantContextAsync(
            id,
            query,
            maxEntities > 0 ? maxEntities : 10,
            maxFindings > 0 ? maxFindings : 5,
            maxFacts > 0 ? maxFacts : 10);

        return TypedResults.Ok(new WorkspaceContextResponse(
            context.CustomInstructions,
            context.Entities.Select(e => new EntityContextDto(
                e.Id, e.Name, e.Type, e.Description, e.Aliases, e.Confidence)).ToList(),
            context.Findings.Select(f => new FindingContextDto(
                f.Id, f.Title, f.Content, f.Type)).ToList(),
            context.Facts.Select(f => new FactContextDto(
                f.Id, f.Statement, f.Confidence)).ToList(),
            context.FormatAsPromptContext()));
    }

    private static WorkspaceResponse ToResponse(Workspace workspace) =>
        new(workspace.Id, workspace.Name, workspace.Description, workspace.TeamId, workspace.CreatedBy, workspace.Status.ToString());
}

public record CreateWorkspaceRequest(string Name, Guid CreatedBy, string? Description = null, Guid? TeamId = null);
public record UpdateWorkspaceRequest(string Name, string? Description = null);
public record WorkspaceResponse(Guid Id, string Name, string? Description, Guid? TeamId, Guid CreatedBy, string Status);
public record WorkspaceContextResponse(
    string? CustomInstructions,
    List<EntityContextDto> Entities,
    List<FindingContextDto> Findings,
    List<FactContextDto> Facts,
    string FormattedContext);
public record EntityContextDto(Guid Id, string Name, string Type, string? Description, List<string> Aliases, string Confidence);
public record FindingContextDto(Guid Id, string Title, string Content, string Type);
public record FactContextDto(Guid Id, string Statement, string Confidence);
