using Aegis.Domain.Common;
using Aegis.Domain.Entities;
using Aegis.Domain.Repositories;
using Aegis.Domain.Services;
using MediatR;

namespace Aegis.Api.Features.Authentication.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly ILogger<LoginCommandHandler> _logger;

    public LoginCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        ILogger<LoginCommandHandler> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _logger = logger;
    }

    public async Task<Result<LoginResponse>> Handle(
        LoginCommand command,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByEmailAsync(command.Email, cancellationToken);

        if (user is null)
        {
            _logger.LogWarning("Login attempt for non-existent user: {Email}", command.Email);
            return Result<LoginResponse>.Failure(UserErrors.InvalidCredentials);
        }

        if (user.IsLockedOut)
        {
            _logger.LogWarning("Login attempt for locked account: {Email}", command.Email);
            return Result<LoginResponse>.Failure(UserErrors.AccountLocked);
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Login attempt for inactive account: {Email}", command.Email);
            return Result<LoginResponse>.Failure(Error.Unauthorized("User.Inactive", "User account is not active"));
        }

        if (user.PasswordHash is null || !_passwordHasher.Verify(command.Password, user.PasswordHash))
        {
            _logger.LogWarning("Invalid password attempt for user: {Email}", command.Email);
            user.RecordFailedLogin();
            await _userRepository.UpdateAsync(user, cancellationToken);
            return Result<LoginResponse>.Failure(UserErrors.InvalidCredentials);
        }

        user.RecordLogin();
        await _userRepository.UpdateAsync(user, cancellationToken);

        var token = _jwtTokenGenerator.Generate(user);
        var expiresAt = DateTime.UtcNow.AddHours(24);

        _logger.LogInformation("User logged in successfully: {UserId}", user.Id);

        return Result<LoginResponse>.Success(new LoginResponse(token, expiresAt));
    }
}
