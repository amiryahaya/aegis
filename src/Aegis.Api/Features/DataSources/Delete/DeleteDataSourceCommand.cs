using Aegis.Domain.Common;
using MediatR;

namespace Aegis.Api.Features.DataSources.Delete;

public record DeleteDataSourceCommand(Guid Id) : IRequest<Result>;
