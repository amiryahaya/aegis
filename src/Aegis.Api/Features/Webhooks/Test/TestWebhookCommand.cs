using Aegis.Domain.Services;
using MediatR;
using Aegis.Domain.Common;

namespace Aegis.Api.Features.Webhooks.Test;

public record TestWebhookCommand(Guid SubscriptionId) : IRequest<Result<WebhookDelivery>>;
