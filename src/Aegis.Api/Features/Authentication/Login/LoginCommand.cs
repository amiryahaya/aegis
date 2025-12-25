using Aegis.Domain.Common;
using MediatR;

namespace Aegis.Api.Features.Authentication.Login;

public record LoginCommand(string Email, string Password) : IRequest<Result<LoginResponse>>;

public record LoginResponse(string Token, DateTime ExpiresAt);
