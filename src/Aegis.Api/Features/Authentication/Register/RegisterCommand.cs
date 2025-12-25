using Aegis.Domain.Common;
using MediatR;

namespace Aegis.Api.Features.Authentication.Register;

public record RegisterCommand(string Email, string Name, string Password) : IRequest<Result<RegisterResponse>>;

public record RegisterResponse(Guid Id, string Email, string Name);
