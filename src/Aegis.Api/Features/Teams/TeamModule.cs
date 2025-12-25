using Aegis.Domain.Entities;
using Aegis.Domain.Repositories;
using Carter;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Aegis.Api.Features.Teams;

public class TeamModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/teams")
            .WithTags("Teams");

        group.MapPost("/", CreateTeam);
        group.MapGet("/", GetAllTeams);
        group.MapGet("/{id:guid}", GetTeam);
        group.MapPut("/{id:guid}", UpdateTeam);
        group.MapDelete("/{id:guid}", DeleteTeam);

        // Member endpoints
        group.MapPost("/{id:guid}/members", AddMember);
        group.MapDelete("/{id:guid}/members/{userId:guid}", RemoveMember);
    }

    private static async Task<Results<Created<TeamResponse>, BadRequest<ProblemDetails>, Conflict<ProblemDetails>>> CreateTeam(
        CreateTeamRequest request,
        ITeamRepository repository,
        IUserRepository userRepository)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Invalid team name",
                Detail = "Team name is required."
            });
        }

        // Verify owner exists
        if (!await userRepository.ExistsAsync(request.OwnerId))
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Invalid owner",
                Detail = "The specified owner does not exist."
            });
        }

        // Check for duplicate name
        if (await repository.NameExistsAsync(request.Name))
        {
            return TypedResults.Conflict(new ProblemDetails
            {
                Title = "Team name already exists",
                Detail = $"A team with name '{request.Name}' already exists."
            });
        }

        var team = Team.Create(request.Name, request.OwnerId, request.Description);
        await repository.AddAsync(team);

        var response = ToResponse(team);
        return TypedResults.Created($"/api/teams/{team.Id}", response);
    }

    private static async Task<Ok<List<TeamResponse>>> GetAllTeams(ITeamRepository repository)
    {
        var teams = await repository.GetAllAsync();
        var response = teams.Select(ToResponse).ToList();
        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<TeamResponse>, NotFound>> GetTeam(
        Guid id,
        ITeamRepository repository)
    {
        var team = await repository.GetByIdAsync(id);
        if (team is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(ToResponse(team));
    }

    private static async Task<Results<Ok<TeamResponse>, NotFound>> UpdateTeam(
        Guid id,
        UpdateTeamRequest request,
        ITeamRepository repository)
    {
        var team = await repository.GetByIdAsync(id);
        if (team is null)
        {
            return TypedResults.NotFound();
        }

        team.UpdateDetails(request.Name, request.Description);
        await repository.UpdateAsync(team);

        return TypedResults.Ok(ToResponse(team));
    }

    private static async Task<Results<NoContent, NotFound>> DeleteTeam(
        Guid id,
        ITeamRepository repository)
    {
        if (!await repository.ExistsAsync(id))
        {
            return TypedResults.NotFound();
        }

        await repository.DeleteAsync(id);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Created, BadRequest<ProblemDetails>, NotFound>> AddMember(
        Guid id,
        AddMemberRequest request,
        ITeamRepository repository,
        IUserRepository userRepository)
    {
        var team = await repository.GetByIdAsync(id);
        if (team is null)
        {
            return TypedResults.NotFound();
        }

        if (!await userRepository.ExistsAsync(request.UserId))
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Invalid user",
                Detail = "The specified user does not exist."
            });
        }

        if (!Enum.TryParse<TeamRole>(request.Role, true, out var role))
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Invalid role",
                Detail = $"'{request.Role}' is not a valid team role."
            });
        }

        var member = TeamMember.Create(id, request.UserId, role);
        await repository.AddMemberAsync(id, member);

        return TypedResults.Created($"/api/teams/{id}/members/{request.UserId}");
    }

    private static async Task<Results<NoContent, NotFound>> RemoveMember(
        Guid id,
        Guid userId,
        ITeamRepository repository)
    {
        if (!await repository.ExistsAsync(id))
        {
            return TypedResults.NotFound();
        }

        await repository.RemoveMemberAsync(id, userId);
        return TypedResults.NoContent();
    }

    private static TeamResponse ToResponse(Team team) =>
        new(team.Id, team.Name, team.Description, team.CreatedBy, team.IsActive);
}

public record CreateTeamRequest(string Name, Guid OwnerId, string? Description = null);
public record UpdateTeamRequest(string Name, string? Description = null);
public record AddMemberRequest(Guid UserId, string Role);
public record TeamResponse(Guid Id, string Name, string? Description, Guid CreatedBy, bool IsActive);
