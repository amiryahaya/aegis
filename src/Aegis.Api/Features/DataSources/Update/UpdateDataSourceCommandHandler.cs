using Aegis.Api.Features.DataSources.Get;
using Aegis.Domain.Common;
using Aegis.Domain.Entities;
using Aegis.Domain.Repositories;
using MediatR;

namespace Aegis.Api.Features.DataSources.Update;

public class UpdateDataSourceCommandHandler : IRequestHandler<UpdateDataSourceCommand, Result<DataSourceResponse>>
{
    private readonly IDataSourceRepository _dataSourceRepository;
    private readonly ILogger<UpdateDataSourceCommandHandler> _logger;

    public UpdateDataSourceCommandHandler(
        IDataSourceRepository dataSourceRepository,
        ILogger<UpdateDataSourceCommandHandler> logger)
    {
        _dataSourceRepository = dataSourceRepository;
        _logger = logger;
    }

    public async Task<Result<DataSourceResponse>> Handle(
        UpdateDataSourceCommand command,
        CancellationToken cancellationToken)
    {
        var dataSource = await _dataSourceRepository.GetByIdAsync(command.Id, cancellationToken);

        if (dataSource is null)
        {
            return Result<DataSourceResponse>.Failure(DataSourceErrors.NotFound);
        }

        dataSource.UpdateDetails(command.Name, command.Description);

        await _dataSourceRepository.UpdateAsync(dataSource, cancellationToken);

        _logger.LogInformation("Updated data source {DataSourceId}", command.Id);

        var response = new DataSourceResponse(
            dataSource.Id,
            dataSource.Name,
            dataSource.Description,
            dataSource.TeamId,
            dataSource.WorkspaceId,
            dataSource.Type.ToString(),
            dataSource.Status.ToString(),
            dataSource.CreatedBy,
            dataSource.DocumentCount,
            dataSource.TotalSizeBytes,
            dataSource.LastIndexedAt,
            dataSource.CreatedAt);

        return Result<DataSourceResponse>.Success(response);
    }
}
