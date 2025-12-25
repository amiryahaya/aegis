using Aegis.Api.Features.Authentication.Register;
using Aegis.Domain.Entities;
using Aegis.Domain.Repositories;
using Aegis.Domain.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Aegis.UnitTests.Features.Authentication;

public class RegisterCommandHandlerTests
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<RegisterCommandHandler> _logger;
    private readonly RegisterCommandHandler _handler;

    public RegisterCommandHandlerTests()
    {
        _userRepository = Substitute.For<IUserRepository>();
        _passwordHasher = Substitute.For<IPasswordHasher>();
        _logger = Substitute.For<ILogger<RegisterCommandHandler>>();
        _handler = new RegisterCommandHandler(_userRepository, _passwordHasher, _logger);
    }

    [Fact]
    public async Task Handle_WithValidData_ShouldCreateUser()
    {
        // Arrange
        var command = new RegisterCommand("test@example.com", "Test User", "password123");
        _userRepository.EmailExistsAsync(command.Email, Arg.Any<CancellationToken>()).Returns(false);
        _passwordHasher.Hash(command.Password).Returns("hashed_password");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be("test@example.com");
        result.Value.Name.Should().Be("Test User");
        await _userRepository.Received(1).AddAsync(Arg.Is<User>(u =>
            u.Email == "test@example.com" &&
            u.Name == "Test User" &&
            u.PasswordHash == "hashed_password"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithExistingEmail_ShouldReturnFailure()
    {
        // Arrange
        var command = new RegisterCommand("existing@example.com", "Test User", "password123");
        _userRepository.EmailExistsAsync(command.Email, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.EmailAlreadyExists);
        await _userRepository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithValidData_ShouldHashPassword()
    {
        // Arrange
        var command = new RegisterCommand("test@example.com", "Test User", "password123");
        _userRepository.EmailExistsAsync(command.Email, Arg.Any<CancellationToken>()).Returns(false);
        _passwordHasher.Hash(command.Password).Returns("hashed_password");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _passwordHasher.Received(1).Hash("password123");
        await _userRepository.Received(1).AddAsync(Arg.Is<User>(u => u.PasswordHash == "hashed_password"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithValidData_ShouldSetDefaultRole()
    {
        // Arrange
        var command = new RegisterCommand("test@example.com", "Test User", "password123");
        _userRepository.EmailExistsAsync(command.Email, Arg.Any<CancellationToken>()).Returns(false);
        _passwordHasher.Hash(command.Password).Returns("hashed_password");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _userRepository.Received(1).AddAsync(Arg.Is<User>(u => u.Role == UserRole.Viewer), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithValidData_ShouldSetUserAsActiveAndUnverified()
    {
        // Arrange
        var command = new RegisterCommand("test@example.com", "Test User", "password123");
        _userRepository.EmailExistsAsync(command.Email, Arg.Any<CancellationToken>()).Returns(false);
        _passwordHasher.Hash(command.Password).Returns("hashed_password");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _userRepository.Received(1).AddAsync(Arg.Is<User>(u =>
            u.IsActive == true &&
            u.EmailVerified == false),
            Arg.Any<CancellationToken>());
    }
}
