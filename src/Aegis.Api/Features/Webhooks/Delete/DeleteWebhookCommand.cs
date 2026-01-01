using MediatR;
using Aegis.Domain.Common;

namespace Aegis.Api.Features.Webhooks.Delete;

public record DeleteWebhookCommand(Guid SubscriptionId) : IRequest<Result>;
