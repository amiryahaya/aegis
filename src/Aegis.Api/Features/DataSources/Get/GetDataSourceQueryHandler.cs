using Aegis.Domain.Common;
using Aegis.Domain.Entities;
using Aegis.Domain.Repositories;
using MediatR;

namespace Aegis.Api.Features.DataSources.Get;

public class GetDataSourceQueryHandler : IRequestHandler<GetDataSourceQuery, Result<DataSourceResponse>>
{
    private readonly IDataSourceRepository _dataSourceRepository;

    public GetDataSourceQueryHandler(IDataSourceRepository dataSourceRepository)
    {
        _dataSourceRepository = dataSourceRepository;
    }

    public async Task<Result<DataSourceResponse>> Handle(
        GetDataSourceQuery query,
        CancellationToken cancellationToken)
    {
        var dataSource = await _dataSourceRepository.GetByIdAsync(query.Id, cancellationToken);

        if (dataSource is null)
        {
            return Result<DataSourceResponse>.Failure(DataSourceErrors.NotFound);
        }

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
