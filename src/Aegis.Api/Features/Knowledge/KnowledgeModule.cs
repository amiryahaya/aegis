using Aegis.Domain.Entities;
using Aegis.Domain.Repositories;
using Carter;
using Microsoft.AspNetCore.Mvc;

namespace Aegis.Api.Features.Knowledge;

public class KnowledgeModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/workspaces/{workspaceId:guid}/knowledge")
            .WithTags("Knowledge");

        // Entities
        group.MapPost("/entities", CreateEntity);
        group.MapGet("/entities", GetEntities);
        group.MapGet("/entities/{id:guid}", GetEntity);
        group.MapPut("/entities/{id:guid}", UpdateEntity);
        group.MapDelete("/entities/{id:guid}", DeleteEntity);

        // Findings
        group.MapPost("/findings", CreateFinding);
        group.MapGet("/findings", GetFindings);
        group.MapGet("/findings/{id:guid}", GetFinding);
        group.MapPut("/findings/{id:guid}", UpdateFinding);
        group.MapDelete("/findings/{id:guid}", DeleteFinding);

        // Facts
        group.MapPost("/facts", CreateFact);
        group.MapGet("/facts", GetFacts);
        group.MapGet("/facts/{id:guid}", GetFact);
        group.MapPut("/facts/{id:guid}", UpdateFact);
        group.MapDelete("/facts/{id:guid}", DeleteFact);
    }

    // ======================== ENTITIES ========================

    private static async Task<IResult> CreateEntity(
        Guid workspaceId,
        [FromBody] CreateEntityRequest request,
        IWorkspaceRepository workspaceRepository,
        IWorkspaceEntityRepository entityRepository)
    {
        // Verify workspace exists
        var workspace = await workspaceRepository.GetByIdAsync(workspaceId);
        if (workspace is null)
            return Results.NotFound(new { error = "Workspace not found" });

        var entity = WorkspaceEntity.Create(
            workspaceId,
            request.Name,
            request.Type,
            request.AddedBy,
            request.Description,
            request.Aliases,
            request.Confidence ?? "Medium",
            request.SourceConversationId);

        var id = await entityRepository.CreateAsync(entity);

        return Results.Created($"/api/workspaces/{workspaceId}/knowledge/entities/{id}", new { id });
    }

    private static async Task<IResult> GetEntities(
        Guid workspaceId,
        [FromQuery] string? type,
        IWorkspaceEntityRepository entityRepository)
    {
        var entities = type is not null
            ? await entityRepository.GetByTypeAsync(workspaceId, type)
            : await entityRepository.GetByWorkspaceIdAsync(workspaceId);

        return Results.Ok(entities.Select(e => new
        {
            e.Id,
            e.WorkspaceId,
            e.Name,
            e.Type,
            e.Description,
            e.Aliases,
            e.Confidence,
            e.SourceConversationId,
            e.AddedBy,
            e.CreatedAt,
            e.UpdatedAt
        }));
    }

    private static async Task<IResult> GetEntity(
        Guid workspaceId,
        Guid id,
        IWorkspaceEntityRepository entityRepository)
    {
        var entity = await entityRepository.GetByIdAsync(id);
        if (entity is null || entity.WorkspaceId != workspaceId)
            return Results.NotFound(new { error = "Entity not found" });

        return Results.Ok(new
        {
            entity.Id,
            entity.WorkspaceId,
            entity.Name,
            entity.Type,
            entity.Description,
            entity.Aliases,
            entity.Confidence,
            entity.SourceConversationId,
            entity.AddedBy,
            entity.CreatedAt,
            entity.UpdatedAt
        });
    }

    private static async Task<IResult> UpdateEntity(
        Guid workspaceId,
        Guid id,
        [FromBody] UpdateEntityRequest request,
        IWorkspaceEntityRepository entityRepository)
    {
        var entity = await entityRepository.GetByIdAsync(id);
        if (entity is null || entity.WorkspaceId != workspaceId)
            return Results.NotFound(new { error = "Entity not found" });

        entity.UpdateDetails(request.Name, request.Description, request.Aliases);

        if (request.Confidence is not null)
            entity.UpdateConfidence(request.Confidence);

        await entityRepository.UpdateAsync(entity);

        return Results.NoContent();
    }

    private static async Task<IResult> DeleteEntity(
        Guid workspaceId,
        Guid id,
        IWorkspaceEntityRepository entityRepository)
    {
        var entity = await entityRepository.GetByIdAsync(id);
        if (entity is null || entity.WorkspaceId != workspaceId)
            return Results.NotFound(new { error = "Entity not found" });

        await entityRepository.DeleteAsync(id);

        return Results.NoContent();
    }

    // ======================== FINDINGS ========================

    private static async Task<IResult> CreateFinding(
        Guid workspaceId,
        [FromBody] CreateFindingRequest request,
        IWorkspaceRepository workspaceRepository,
        IWorkspaceFindingRepository findingRepository)
    {
        // Verify workspace exists
        var workspace = await workspaceRepository.GetByIdAsync(workspaceId);
        if (workspace is null)
            return Results.NotFound(new { error = "Workspace not found" });

        var finding = WorkspaceFinding.Create(
            workspaceId,
            request.Title,
            request.Content,
            request.AddedBy,
            request.Type ?? "Evidence",
            request.SupportingEntityIds,
            request.SourceDocumentIds,
            request.SourceConversationId);

        var id = await findingRepository.CreateAsync(finding);

        return Results.Created($"/api/workspaces/{workspaceId}/knowledge/findings/{id}", new { id });
    }

    private static async Task<IResult> GetFindings(
        Guid workspaceId,
        [FromQuery] string? type,
        IWorkspaceFindingRepository findingRepository)
    {
        var findings = type is not null
            ? await findingRepository.GetByTypeAsync(workspaceId, type)
            : await findingRepository.GetByWorkspaceIdAsync(workspaceId);

        return Results.Ok(findings.Select(f => new
        {
            f.Id,
            f.WorkspaceId,
            f.Title,
            f.Content,
            f.Type,
            f.SupportingEntityIds,
            f.SourceDocumentIds,
            f.SourceConversationId,
            f.AddedBy,
            f.CreatedAt,
            f.UpdatedAt
        }));
    }

    private static async Task<IResult> GetFinding(
        Guid workspaceId,
        Guid id,
        IWorkspaceFindingRepository findingRepository)
    {
        var finding = await findingRepository.GetByIdAsync(id);
        if (finding is null || finding.WorkspaceId != workspaceId)
            return Results.NotFound(new { error = "Finding not found" });

        return Results.Ok(new
        {
            finding.Id,
            finding.WorkspaceId,
            finding.Title,
            finding.Content,
            finding.Type,
            finding.SupportingEntityIds,
            finding.SourceDocumentIds,
            finding.SourceConversationId,
            finding.AddedBy,
            finding.CreatedAt,
            finding.UpdatedAt
        });
    }

    private static async Task<IResult> UpdateFinding(
        Guid workspaceId,
        Guid id,
        [FromBody] UpdateFindingRequest request,
        IWorkspaceFindingRepository findingRepository)
    {
        var finding = await findingRepository.GetByIdAsync(id);
        if (finding is null || finding.WorkspaceId != workspaceId)
            return Results.NotFound(new { error = "Finding not found" });

        finding.UpdateContent(request.Title, request.Content);

        await findingRepository.UpdateAsync(finding);

        return Results.NoContent();
    }

    private static async Task<IResult> DeleteFinding(
        Guid workspaceId,
        Guid id,
        IWorkspaceFindingRepository findingRepository)
    {
        var finding = await findingRepository.GetByIdAsync(id);
        if (finding is null || finding.WorkspaceId != workspaceId)
            return Results.NotFound(new { error = "Finding not found" });

        await findingRepository.DeleteAsync(id);

        return Results.NoContent();
    }

    // ======================== FACTS ========================

    private static async Task<IResult> CreateFact(
        Guid workspaceId,
        [FromBody] CreateFactRequest request,
        IWorkspaceRepository workspaceRepository,
        IWorkspaceFactRepository factRepository)
    {
        // Verify workspace exists
        var workspace = await workspaceRepository.GetByIdAsync(workspaceId);
        if (workspace is null)
            return Results.NotFound(new { error = "Workspace not found" });

        var fact = WorkspaceFact.Create(
            workspaceId,
            request.Statement,
            request.AddedBy,
            request.Confidence ?? "Confirmed",
            request.SourceDocumentIds,
            request.SourceConversationId);

        var id = await factRepository.CreateAsync(fact);

        return Results.Created($"/api/workspaces/{workspaceId}/knowledge/facts/{id}", new { id });
    }

    private static async Task<IResult> GetFacts(
        Guid workspaceId,
        [FromQuery] string? confidence,
        IWorkspaceFactRepository factRepository)
    {
        var facts = confidence is not null
            ? await factRepository.GetByConfidenceAsync(workspaceId, confidence)
            : await factRepository.GetByWorkspaceIdAsync(workspaceId);

        return Results.Ok(facts.Select(f => new
        {
            f.Id,
            f.WorkspaceId,
            f.Statement,
            f.Confidence,
            f.SourceDocumentIds,
            f.SourceConversationId,
            f.AddedBy,
            f.CreatedAt,
            f.UpdatedAt
        }));
    }

    private static async Task<IResult> GetFact(
        Guid workspaceId,
        Guid id,
        IWorkspaceFactRepository factRepository)
    {
        var fact = await factRepository.GetByIdAsync(id);
        if (fact is null || fact.WorkspaceId != workspaceId)
            return Results.NotFound(new { error = "Fact not found" });

        return Results.Ok(new
        {
            fact.Id,
            fact.WorkspaceId,
            fact.Statement,
            fact.Confidence,
            fact.SourceDocumentIds,
            fact.SourceConversationId,
            fact.AddedBy,
            fact.CreatedAt,
            fact.UpdatedAt
        });
    }

    private static async Task<IResult> UpdateFact(
        Guid workspaceId,
        Guid id,
        [FromBody] UpdateFactRequest request,
        IWorkspaceFactRepository factRepository)
    {
        var fact = await factRepository.GetByIdAsync(id);
        if (fact is null || fact.WorkspaceId != workspaceId)
            return Results.NotFound(new { error = "Fact not found" });

        fact.UpdateStatement(request.Statement);

        if (request.Confidence is not null)
            fact.UpdateConfidence(request.Confidence);

        await factRepository.UpdateAsync(fact);

        return Results.NoContent();
    }

    private static async Task<IResult> DeleteFact(
        Guid workspaceId,
        Guid id,
        IWorkspaceFactRepository factRepository)
    {
        var fact = await factRepository.GetByIdAsync(id);
        if (fact is null || fact.WorkspaceId != workspaceId)
            return Results.NotFound(new { error = "Fact not found" });

        await factRepository.DeleteAsync(id);

        return Results.NoContent();
    }
}

// ======================== REQUEST DTOS ========================

public record CreateEntityRequest(
    string Name,
    string Type,
    Guid AddedBy,
    string? Description = null,
    List<string>? Aliases = null,
    string? Confidence = null,
    Guid? SourceConversationId = null);

public record UpdateEntityRequest(
    string Name,
    string? Description,
    List<string>? Aliases = null,
    string? Confidence = null);

public record CreateFindingRequest(
    string Title,
    string Content,
    Guid AddedBy,
    string? Type = null,
    List<Guid>? SupportingEntityIds = null,
    List<Guid>? SourceDocumentIds = null,
    Guid? SourceConversationId = null);

public record UpdateFindingRequest(
    string Title,
    string Content);

public record CreateFactRequest(
    string Statement,
    Guid AddedBy,
    string? Confidence = null,
    List<Guid>? SourceDocumentIds = null,
    Guid? SourceConversationId = null);

public record UpdateFactRequest(
    string Statement,
    string? Confidence = null);
