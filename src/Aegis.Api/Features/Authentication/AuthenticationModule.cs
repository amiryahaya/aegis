using Aegis.Api.Features.Authentication.Login;
using Aegis.Api.Features.Authentication.Register;
using Carter;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Aegis.Api.Features.Authentication;

public class AuthenticationModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Authentication");

        group.MapPost("/register", Register)
            .WithSummary("Register a new user");

        group.MapPost("/login", Login)
            .WithSummary("Login with email and password");
    }

    private static async Task<Results<Created<RegisterResponse>, BadRequest<ProblemDetails>, Conflict<ProblemDetails>>> Register(
        RegisterCommand command,
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

        return TypedResults.Created($"/api/users/{result.Value.Id}", result.Value);
    }

    private static async Task<Results<Ok<LoginResponse>, BadRequest<ProblemDetails>, UnauthorizedHttpResult>> Login(
        LoginCommand command,
        ISender sender)
    {
        var result = await sender.Send(command);

        if (result.IsFailure)
        {
            if (result.Error!.Code.Contains("Unauthorized"))
            {
                return TypedResults.Unauthorized();
            }

            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = result.Error.Code,
                Detail = result.Error.Message
            });
        }

        return TypedResults.Ok(result.Value);
    }
}
