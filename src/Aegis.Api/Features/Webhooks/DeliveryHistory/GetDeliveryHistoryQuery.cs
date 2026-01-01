using Aegis.Domain.Services;
using MediatR;
using Aegis.Domain.Common;

namespace Aegis.Api.Features.Webhooks.DeliveryHistory;

public record GetDeliveryHistoryQuery(Guid SubscriptionId, int Limit = 50)
    : IRequest<Result<IReadOnlyList<WebhookDelivery>>>;
