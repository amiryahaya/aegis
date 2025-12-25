using Aegis.Domain.Common;
using MediatR;

namespace Aegis.Api.Features.DataSources.Get;

public record GetDataSourceQuery(Guid Id) : IRequest<Result<DataSourceResponse>>;

public record DataSourceResponse(
    Guid Id,
    string Name,
    string? Description,
    Guid TeamId,
    Guid? WorkspaceId,
    string Type,
    string Status,
    Guid CreatedBy,
    int DocumentCount,
    long TotalSizeBytes,
    DateTime? LastIndexedAt,
    DateTime CreatedAt);
