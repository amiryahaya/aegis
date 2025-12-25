using Aegis.Api.Features.DataSources.Get;
using Aegis.Domain.Common;
using Aegis.Domain.Repositories;
using MediatR;

namespace Aegis.Api.Features.DataSources.GetByTeam;

public class GetDataSourcesByTeamQueryHandler : IRequestHandler<GetDataSourcesByTeamQuery, Result<List<DataSourceResponse>>>
{
    private readonly IDataSourceRepository _dataSourceRepository;

    public GetDataSourcesByTeamQueryHandler(IDataSourceRepository dataSourceRepository)
    {
        _dataSourceRepository = dataSourceRepository;
    }

    public async Task<Result<List<DataSourceResponse>>> Handle(
        GetDataSourcesByTeamQuery query,
        CancellationToken cancellationToken)
    {
        var dataSources = await _dataSourceRepository.GetByTeamIdAsync(query.TeamId, cancellationToken);

        var responses = dataSources.Select(ds => new DataSourceResponse(
            ds.Id,
            ds.Name,
            ds.Description,
            ds.TeamId,
            ds.WorkspaceId,
            ds.Type.ToString(),
            ds.Status.ToString(),
            ds.CreatedBy,
            ds.DocumentCount,
            ds.TotalSizeBytes,
            ds.LastIndexedAt,
            ds.CreatedAt)).ToList();

        return Result<List<DataSourceResponse>>.Success(responses);
    }
}
