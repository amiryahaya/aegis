using Aegis.Domain.Common;
using Aegis.Domain.Services;
using MediatR;

namespace Aegis.Api.Features.Webhooks.Get;

public class GetWebhookQueryHandler : IRequestHandler<GetWebhookQuery, Result<WebhookSubscription>>
{
    private readonly IWebhookService _webhookService;

    public GetWebhookQueryHandler(IWebhookService webhookService)
    {
        _webhookService = webhookService;
    }

    public async Task<Result<WebhookSubscription>> Handle(
        GetWebhookQuery request,
        CancellationToken cancellationToken)
    {
        return await _webhookService.GetByIdAsync(request.SubscriptionId, cancellationToken);
    }
}
