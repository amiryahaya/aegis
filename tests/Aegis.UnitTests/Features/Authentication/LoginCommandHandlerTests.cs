using Aegis.Api.Features.Authentication.Login;
using Aegis.Domain.Entities;
using Aegis.Domain.Repositories;
using Aegis.Domain.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Aegis.UnitTests.Features.Authentication;

public class LoginCommandHandlerTests
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly ILogger<LoginCommandHandler> _logger;
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        _userRepository = Substitute.For<IUserRepository>();
        _passwordHasher = Substitute.For<IPasswordHasher>();
        _jwtTokenGenerator = Substitute.For<IJwtTokenGenerator>();
        _logger = Substitute.For<ILogger<LoginCommandHandler>>();
        _handler = new LoginCommandHandler(_userRepository, _passwordHasher, _jwtTokenGenerator, _logger);
    }

    [Fact]
    public async Task Handle_WithValidCredentials_ShouldReturnToken()
    {
        // Arrange
        var command = new LoginCommand("test@example.com", "password123");
        var user = User.Create("test@example.com", "Test User");
        user.SetPasswordHash("hashed_password");

        _userRepository.GetByEmailAsync(command.Email, Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify(command.Password, user.PasswordHash!).Returns(true);
        _jwtTokenGenerator.Generate(user).Returns("jwt-token-12345");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Token.Should().Be("jwt-token-12345");
        result.Value.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddHours(24), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Handle_WithInvalidEmail_ShouldReturnFailure()
    {
        // Arrange
        var command = new LoginCommand("unknown@example.com", "password123");
        _userRepository.GetByEmailAsync(command.Email, Arg.Any<CancellationToken>()).Returns((User?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.InvalidCredentials);
    }

    [Fact]
    public async Task Handle_WithInvalidPassword_ShouldReturnFailure()
    {
        // Arrange
        var command = new LoginCommand("test@example.com", "wrongpassword");
        var user = User.Create("test@example.com", "Test User");
        user.SetPasswordHash("hashed_password");

        _userRepository.GetByEmailAsync(command.Email, Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify(command.Password, user.PasswordHash!).Returns(false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.InvalidCredentials);
        await _userRepository.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithLockedAccount_ShouldReturnFailure()
    {
        // Arrange
        var command = new LoginCommand("test@example.com", "password123");
        var user = User.Create("test@example.com", "Test User");
        user.SetPasswordHash("hashed_password");
        user.RecordFailedLogin(maxAttempts: 3);
        user.RecordFailedLogin(maxAttempts: 3);
        user.RecordFailedLogin(maxAttempts: 3);

        _userRepository.GetByEmailAsync(command.Email, Arg.Any<CancellationToken>()).Returns(user);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.AccountLocked);
    }

    [Fact]
    public async Task Handle_WithInactiveAccount_ShouldReturnFailure()
    {
        // Arrange
        var command = new LoginCommand("test@example.com", "password123");
        var user = User.Create("test@example.com", "Test User");
        user.SetPasswordHash("hashed_password");
        user.Deactivate();

        _userRepository.GetByEmailAsync(command.Email, Arg.Any<CancellationToken>()).Returns(user);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("Unauthorized");
    }

    [Fact]
    public async Task Handle_WithValidCredentials_ShouldRecordLogin()
    {
        // Arrange
        var command = new LoginCommand("test@example.com", "password123");
        var user = User.Create("test@example.com", "Test User");
        user.SetPasswordHash("hashed_password");

        _userRepository.GetByEmailAsync(command.Email, Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify(command.Password, user.PasswordHash!).Returns(true);
        _jwtTokenGenerator.Generate(user).Returns("jwt-token-12345");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        user.LastLoginAt.Should().NotBeNull();
        user.FailedLoginAttempts.Should().Be(0);
        await _userRepository.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
    }
}
