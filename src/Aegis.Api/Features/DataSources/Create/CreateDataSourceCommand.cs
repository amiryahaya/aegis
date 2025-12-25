using Aegis.Domain.Common;
using MediatR;

namespace Aegis.Api.Features.DataSources.Create;

public record CreateDataSourceCommand(
    string Name,
    Guid TeamId,
    Guid CreatedBy,
    string? Description = null,
    string Type = "Upload",
    Guid? WorkspaceId = null) : IRequest<Result<CreateDataSourceResponse>>;

public record CreateDataSourceResponse(
    Guid Id,
    string Name,
    string? Description,
    Guid TeamId,
    Guid? WorkspaceId,
    string Type,
    string Status,
    Guid CreatedBy);
