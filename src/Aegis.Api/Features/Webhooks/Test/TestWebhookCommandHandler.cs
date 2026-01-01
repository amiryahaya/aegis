using Aegis.Domain.Common;
using Aegis.Domain.Services;
using MediatR;

namespace Aegis.Api.Features.Webhooks.Test;

public class TestWebhookCommandHandler : IRequestHandler<TestWebhookCommand, Result<WebhookDelivery>>
{
    private readonly IWebhookService _webhookService;

    public TestWebhookCommandHandler(IWebhookService webhookService)
    {
        _webhookService = webhookService;
    }

    public async Task<Result<WebhookDelivery>> Handle(
        TestWebhookCommand request,
        CancellationToken cancellationToken)
    {
        return await _webhookService.TestAsync(request.SubscriptionId, cancellationToken);
    }
}
