using Aegis.Domain.Entities;
using Aegis.Domain.Repositories;
using Carter;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Aegis.Api.Features.Users;

public class UserModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users")
            .WithTags("Users");

        group.MapPost("/", CreateUser);
        group.MapGet("/", GetAllUsers);
        group.MapGet("/{id:guid}", GetUser);
        group.MapPut("/{id:guid}", UpdateUser);
        group.MapDelete("/{id:guid}", DeleteUser);
    }

    private static async Task<Results<Created<UserResponse>, BadRequest<ProblemDetails>, Conflict<ProblemDetails>>> CreateUser(
        CreateUserRequest request,
        IUserRepository repository)
    {
        // Validate email format
        if (!IsValidEmail(request.Email))
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Invalid email format",
                Detail = "The provided email address is not valid."
            });
        }

        // Check for duplicate email
        if (await repository.EmailExistsAsync(request.Email))
        {
            return TypedResults.Conflict(new ProblemDetails
            {
                Title = "Email already exists",
                Detail = $"A user with email '{request.Email}' already exists."
            });
        }

        var user = User.Create(request.Email, request.Name);
        await repository.AddAsync(user);

        var response = ToResponse(user);
        return TypedResults.Created($"/api/users/{user.Id}", response);
    }

    private static async Task<Ok<List<UserResponse>>> GetAllUsers(IUserRepository repository)
    {
        var users = await repository.GetAllAsync();
        var response = users.Select(ToResponse).ToList();
        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<UserResponse>, NotFound>> GetUser(
        Guid id,
        IUserRepository repository)
    {
        var user = await repository.GetByIdAsync(id);
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(ToResponse(user));
    }

    private static async Task<Results<Ok<UserResponse>, NotFound>> UpdateUser(
        Guid id,
        UpdateUserRequest request,
        IUserRepository repository)
    {
        var user = await repository.GetByIdAsync(id);
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        user.UpdateProfile(request.Name);
        await repository.UpdateAsync(user);

        return TypedResults.Ok(ToResponse(user));
    }

    private static async Task<Results<NoContent, NotFound>> DeleteUser(
        Guid id,
        IUserRepository repository)
    {
        if (!await repository.ExistsAsync(id))
        {
            return TypedResults.NotFound();
        }

        await repository.DeleteAsync(id);
        return TypedResults.NoContent();
    }

    private static UserResponse ToResponse(User user) =>
        new(user.Id, user.Email, user.Name, user.Role.ToString(), user.IsActive);

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}

public record CreateUserRequest(string Email, string Name);
public record UpdateUserRequest(string Name);
public record UserResponse(Guid Id, string Email, string Name, string Role, bool IsActive);
