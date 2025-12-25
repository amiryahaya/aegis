using Aegis.Api.Features.DataSources.Get;
using Aegis.Domain.Common;
using MediatR;

namespace Aegis.Api.Features.DataSources.GetByTeam;

public record GetDataSourcesByTeamQuery(Guid TeamId) : IRequest<Result<List<DataSourceResponse>>>;
