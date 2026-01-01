using Aegis.Domain.Common;
using Aegis.Domain.Services;
using MediatR;

namespace Aegis.Api.Features.Webhooks.Update;

public class UpdateWebhookCommandHandler : IRequestHandler<UpdateWebhookCommand, Result<WebhookSubscription>>
{
    private readonly IWebhookService _webhookService;

    public UpdateWebhookCommandHandler(IWebhookService webhookService)
    {
        _webhookService = webhookService;
    }

    public async Task<Result<WebhookSubscription>> Handle(
        UpdateWebhookCommand request,
        CancellationToken cancellationToken)
    {
        var update = new WebhookUpdate
        {
            Name = request.Name,
            Url = request.Url,
            Description = request.Description,
            Events = request.Events,
            Secret = request.Secret,
            Headers = request.Headers,
            IsActive = request.IsActive
        };

        return await _webhookService.UpdateAsync(request.SubscriptionId, update, cancellationToken);
    }
}
