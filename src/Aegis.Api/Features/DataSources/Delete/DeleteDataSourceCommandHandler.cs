using Aegis.Domain.Common;
using Aegis.Domain.Entities;
using Aegis.Domain.Repositories;
using MediatR;

namespace Aegis.Api.Features.DataSources.Delete;

public class DeleteDataSourceCommandHandler : IRequestHandler<DeleteDataSourceCommand, Result>
{
    private readonly IDataSourceRepository _dataSourceRepository;
    private readonly ILogger<DeleteDataSourceCommandHandler> _logger;

    public DeleteDataSourceCommandHandler(
        IDataSourceRepository dataSourceRepository,
        ILogger<DeleteDataSourceCommandHandler> logger)
    {
        _dataSourceRepository = dataSourceRepository;
        _logger = logger;
    }

    public async Task<Result> Handle(
        DeleteDataSourceCommand command,
        CancellationToken cancellationToken)
    {
        if (!await _dataSourceRepository.ExistsAsync(command.Id, cancellationToken))
        {
            return Result.Failure(DataSourceErrors.NotFound);
        }

        await _dataSourceRepository.DeleteAsync(command.Id, cancellationToken);

        _logger.LogInformation("Deleted data source {DataSourceId}", command.Id);

        return Result.Success();
    }
}
