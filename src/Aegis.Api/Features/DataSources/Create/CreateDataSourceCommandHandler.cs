using Aegis.Domain.Common;
using Aegis.Domain.Entities;
using Aegis.Domain.Repositories;
using MediatR;

namespace Aegis.Api.Features.DataSources.Create;

public class CreateDataSourceCommandHandler : IRequestHandler<CreateDataSourceCommand, Result<CreateDataSourceResponse>>
{
    private readonly IDataSourceRepository _dataSourceRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly ILogger<CreateDataSourceCommandHandler> _logger;

    public CreateDataSourceCommandHandler(
        IDataSourceRepository dataSourceRepository,
        ITeamRepository teamRepository,
        ILogger<CreateDataSourceCommandHandler> logger)
    {
        _dataSourceRepository = dataSourceRepository;
        _teamRepository = teamRepository;
        _logger = logger;
    }

    public async Task<Result<CreateDataSourceResponse>> Handle(
        CreateDataSourceCommand command,
        CancellationToken cancellationToken)
    {
        // Verify team exists
        if (!await _teamRepository.ExistsAsync(command.TeamId, cancellationToken))
        {
            _logger.LogWarning("Attempted to create data source for non-existent team: {TeamId}", command.TeamId);
            return Result<CreateDataSourceResponse>.Failure(
                Error.NotFound("Team.NotFound", "The specified team does not exist"));
        }

        // Check for duplicate name in team
        if (await _dataSourceRepository.NameExistsInTeamAsync(command.Name, command.TeamId, cancellationToken))
        {
            _logger.LogWarning("Data source with name '{Name}' already exists in team {TeamId}", command.Name, command.TeamId);
            return Result<CreateDataSourceResponse>.Failure(
                Error.Conflict("DataSource.DuplicateName", $"A data source with name '{command.Name}' already exists in this team"));
        }

        // Parse data source type
        if (!Enum.TryParse<DataSourceType>(command.Type, ignoreCase: true, out var dataSourceType))
        {
            return Result<CreateDataSourceResponse>.Failure(
                Error.Validation("DataSource.InvalidType", $"Invalid data source type: {command.Type}"));
        }

        // Create data source
        var dataSource = DataSource.Create(
            command.Name,
            command.TeamId,
            command.CreatedBy,
            dataSourceType,
            command.Description,
            command.WorkspaceId);

        await _dataSourceRepository.AddAsync(dataSource, cancellationToken);

        _logger.LogInformation("Created data source {DataSourceId} in team {TeamId}", dataSource.Id, command.TeamId);

        var response = new CreateDataSourceResponse(
            dataSource.Id,
            dataSource.Name,
            dataSource.Description,
            dataSource.TeamId,
            dataSource.WorkspaceId,
            dataSource.Type.ToString(),
            dataSource.Status.ToString(),
            dataSource.CreatedBy);

        return Result<CreateDataSourceResponse>.Success(response);
    }
}
