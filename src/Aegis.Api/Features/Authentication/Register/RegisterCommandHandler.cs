using Aegis.Domain.Common;
using Aegis.Domain.Entities;
using Aegis.Domain.Repositories;
using Aegis.Domain.Services;
using MediatR;

namespace Aegis.Api.Features.Authentication.Register;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, Result<RegisterResponse>>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<RegisterCommandHandler> _logger;

    public RegisterCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ILogger<RegisterCommandHandler> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<Result<RegisterResponse>> Handle(
        RegisterCommand command,
        CancellationToken cancellationToken)
    {
        if (await _userRepository.EmailExistsAsync(command.Email, cancellationToken))
        {
            _logger.LogWarning("Registration attempt with existing email: {Email}", command.Email);
            return Result<RegisterResponse>.Failure(UserErrors.EmailAlreadyExists);
        }

        var user = User.Create(command.Email, command.Name, UserRole.Viewer);
        var passwordHash = _passwordHasher.Hash(command.Password);
        user.SetPasswordHash(passwordHash);

        await _userRepository.AddAsync(user, cancellationToken);

        _logger.LogInformation("New user registered: {UserId}", user.Id);

        return Result<RegisterResponse>.Success(new RegisterResponse(user.Id, user.Email, user.Name));
    }
}
