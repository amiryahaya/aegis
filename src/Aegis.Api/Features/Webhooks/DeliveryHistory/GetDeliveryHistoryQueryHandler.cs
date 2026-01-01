using Aegis.Domain.Common;
using Aegis.Domain.Services;
using MediatR;

namespace Aegis.Api.Features.Webhooks.DeliveryHistory;

public class GetDeliveryHistoryQueryHandler
    : IRequestHandler<GetDeliveryHistoryQuery, Result<IReadOnlyList<WebhookDelivery>>>
{
    private readonly IWebhookService _webhookService;

    public GetDeliveryHistoryQueryHandler(IWebhookService webhookService)
    {
        _webhookService = webhookService;
    }

    public async Task<Result<IReadOnlyList<WebhookDelivery>>> Handle(
        GetDeliveryHistoryQuery request,
        CancellationToken cancellationToken)
    {
        return await _webhookService.GetDeliveryHistoryAsync(
            request.SubscriptionId,
            request.Limit,
            cancellationToken);
    }
}
