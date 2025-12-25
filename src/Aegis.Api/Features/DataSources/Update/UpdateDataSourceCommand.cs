using Aegis.Api.Features.DataSources.Get;
using Aegis.Domain.Common;
using MediatR;

namespace Aegis.Api.Features.DataSources.Update;

public record UpdateDataSourceCommand(
    Guid Id,
    string Name,
    string? Description = null) : IRequest<Result<DataSourceResponse>>;
