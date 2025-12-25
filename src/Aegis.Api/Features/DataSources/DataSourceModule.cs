using Aegis.Api.Features.DataSources.Create;
using Aegis.Api.Features.DataSources.Delete;
using Aegis.Api.Features.DataSources.Get;
using Aegis.Api.Features.DataSources.GetByTeam;
using Aegis.Api.Features.DataSources.Update;
using Carter;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Aegis.Api.Features.DataSources;

public class DataSourceModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/datasources")
            .WithTags("Data Sources");

        group.MapPost("/", CreateDataSource)
            .WithSummary("Create a new data source");

        group.MapGet("/{id:guid}", GetDataSource)
            .WithSummary("Get a data source by ID");

        group.MapGet("/", GetDataSourcesByTeam)
            .WithSummary("Get data sources by team");

        group.MapPut("/{id:guid}", UpdateDataSource)
            .WithSummary("Update a data source");

        group.MapDelete("/{id:guid}", DeleteDataSource)
            .WithSummary("Delete a data source");
    }

    private static async Task<Results<Created<CreateDataSourceResponse>, BadRequest<ProblemDetails>, Conflict<ProblemDetails>>> CreateDataSource(
        CreateDataSourceCommand command,
        ISender sender)
    {
        var result = await sender.Send(command);

        if (result.IsFailure)
        {
            if (result.Error!.Code.Contains("Conflict"))
            {
                return TypedResults.Conflict(new ProblemDetails
                {
                    Title = result.Error.Code,
                    Detail = result.Error.Message
                });
            }

            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = result.Error.Code,
                Detail = result.Error.Message
            });
        }

        return TypedResults.Created($"/api/datasources/{result.Value.Id}", result.Value);
    }

    private static async Task<Results<Ok<DataSourceResponse>, NotFound>> GetDataSource(
        Guid id,
        ISender sender)
    {
        var query = new GetDataSourceQuery(id);
        var result = await sender.Send(query);

        if (result.IsFailure)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(result.Value);
    }

    private static async Task<Ok<List<DataSourceResponse>>> GetDataSourcesByTeam(
        Guid teamId,
        ISender sender)
    {
        var query = new GetDataSourcesByTeamQuery(teamId);
        var result = await sender.Send(query);

        return TypedResults.Ok(result.Value);
    }

    private static async Task<Results<Ok<DataSourceResponse>, NotFound>> UpdateDataSource(
        Guid id,
        UpdateDataSourceRequest request,
        ISender sender)
    {
        var command = new UpdateDataSourceCommand(id, request.Name, request.Description);
        var result = await sender.Send(command);

        if (result.IsFailure)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(result.Value);
    }

    private static async Task<Results<NoContent, NotFound>> DeleteDataSource(
        Guid id,
        ISender sender)
    {
        var command = new DeleteDataSourceCommand(id);
        var result = await sender.Send(command);

        if (result.IsFailure)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.NoContent();
    }
}

public record UpdateDataSourceRequest(string Name, string? Description = null);
