using Aegis.Domain.Services;
using MediatR;
using Aegis.Domain.Common;

namespace Aegis.Api.Features.Webhooks.Get;

public record GetWebhookQuery(Guid SubscriptionId) : IRequest<Result<WebhookSubscription>>;
