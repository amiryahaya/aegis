using Aegis.Api.Features.DataSources.Get;
using Aegis.Domain.Entities;
using Aegis.Domain.Repositories;
using FluentAssertions;
using NSubstitute;

namespace Aegis.UnitTests.Features.DataSources;

public class GetDataSourceQueryHandlerTests
{
    private readonly IDataSourceRepository _dataSourceRepository;
    private readonly GetDataSourceQueryHandler _handler;

    public GetDataSourceQueryHandlerTests()
    {
        _dataSourceRepository = Substitute.For<IDataSourceRepository>();
        _handler = new GetDataSourceQueryHandler(_dataSourceRepository);
    }

    [Fact]
    public async Task Handle_WithExistingId_ShouldReturnDataSource()
    {
        // Arrange
        var dataSourceId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var dataSource = DataSource.Create("Test Source", teamId, Guid.NewGuid());

        _dataSourceRepository.GetByIdAsync(dataSourceId, Arg.Any<CancellationToken>()).Returns(dataSource);

        var query = new GetDataSourceQuery(dataSourceId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Test Source");
        result.Value.TeamId.Should().Be(teamId);
    }

    [Fact]
    public async Task Handle_WithNonExistentId_ShouldReturnFailure()
    {
        // Arrange
        var dataSourceId = Guid.NewGuid();
        _dataSourceRepository.GetByIdAsync(dataSourceId, Arg.Any<CancellationToken>()).Returns((DataSource?)null);

        var query = new GetDataSourceQuery(dataSourceId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DataSourceErrors.NotFound);
    }
}
