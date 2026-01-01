using Aegis.Api.Features.DataSources.Create;
using Aegis.Domain.Entities;
using Aegis.Domain.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Aegis.UnitTests.Features.DataSources;

public class CreateDataSourceCommandHandlerTests
{
    private readonly IDataSourceRepository _dataSourceRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly ILogger<CreateDataSourceCommandHandler> _logger;
    private readonly CreateDataSourceCommandHandler _handler;

    public CreateDataSourceCommandHandlerTests()
    {
        _dataSourceRepository = Substitute.For<IDataSourceRepository>();
        _teamRepository = Substitute.For<ITeamRepository>();
        _logger = Substitute.For<ILogger<CreateDataSourceCommandHandler>>();
        _handler = new CreateDataSourceCommandHandler(_dataSourceRepository, _teamRepository, _logger);
    }

    [Fact]
    public async Task Handle_WithValidData_ShouldCreateDataSource()
    {
        // Arrange
        var teamId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = new CreateDataSourceCommand(
            Name: "Test Data Source",
            TeamId: teamId,
            CreatedBy: userId,
            Description: "Test description",
            Type: "Upload",
            WorkspaceId: null);

        _teamRepository.ExistsAsync(teamId, Arg.Any<CancellationToken>()).Returns(true);
        _dataSourceRepository.NameExistsInTeamAsync(command.Name, teamId, Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Test Data Source");
        result.Value.TeamId.Should().Be(teamId);
        await _dataSourceRepository.Received(1).AddAsync(Arg.Is<DataSource>(ds =>
            ds.Name == "Test Data Source" &&
            ds.TeamId == teamId &&
            ds.CreatedBy == userId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithNonExistentTeam_ShouldReturnFailure()
    {
        // Arrange
        var teamId = Guid.NewGuid();
        var command = new CreateDataSourceCommand(
            Name: "Test Data Source",
            TeamId: teamId,
            CreatedBy: Guid.NewGuid(),
            Description: null,
            Type: "Upload",
            WorkspaceId: null);

        _teamRepository.ExistsAsync(teamId, Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Contain("NotFound");
        await _dataSourceRepository.DidNotReceive().AddAsync(Arg.Any<DataSource>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithDuplicateName_ShouldReturnFailure()
    {
        // Arrange
        var teamId = Guid.NewGuid();
        var command = new CreateDataSourceCommand(
            Name: "Existing Data Source",
            TeamId: teamId,
            CreatedBy: Guid.NewGuid(),
            Description: null,
            Type: "Upload",
            WorkspaceId: null);

        _teamRepository.ExistsAsync(teamId, Arg.Any<CancellationToken>()).Returns(true);
        _dataSourceRepository.NameExistsInTeamAsync(command.Name, teamId, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Contain("Conflict");
        await _dataSourceRepository.DidNotReceive().AddAsync(Arg.Any<DataSource>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithValidWorkspaceId_ShouldAssociateWorkspace()
    {
        // Arrange
        var teamId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var command = new CreateDataSourceCommand(
            Name: "Test Data Source",
            TeamId: teamId,
            CreatedBy: Guid.NewGuid(),
            Description: null,
            Type: "Upload",
            WorkspaceId: workspaceId);

        _teamRepository.ExistsAsync(teamId, Arg.Any<CancellationToken>()).Returns(true);
        _dataSourceRepository.NameExistsInTeamAsync(command.Name, teamId, Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _dataSourceRepository.Received(1).AddAsync(Arg.Is<DataSource>(ds =>
            ds.WorkspaceId == workspaceId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithDifferentDataSourceType_ShouldSetCorrectType()
    {
        // Arrange
        var teamId = Guid.NewGuid();
        var command = new CreateDataSourceCommand(
            Name: "API Data Source",
            TeamId: teamId,
            CreatedBy: Guid.NewGuid(),
            Description: "API source",
            Type: "RestApi",
            WorkspaceId: null);

        _teamRepository.ExistsAsync(teamId, Arg.Any<CancellationToken>()).Returns(true);
        _dataSourceRepository.NameExistsInTeamAsync(command.Name, teamId, Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _dataSourceRepository.Received(1).AddAsync(Arg.Is<DataSource>(ds =>
            ds.Type == DataSourceType.RestApi),
            Arg.Any<CancellationToken>());
    }
}
