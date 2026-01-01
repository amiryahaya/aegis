using Aegis.Domain.Common;
using Aegis.Domain.Services;
using MediatR;

namespace Aegis.Api.Features.Webhooks.List;

public class ListWebhooksQueryHandler : IRequestHandler<ListWebhooksQuery, Result<IReadOnlyList<WebhookSubscription>>>
{
    private readonly IWebhookService _webhookService;

    public ListWebhooksQueryHandler(IWebhookService webhookService)
    {
        _webhookService = webhookService;
    }

    public async Task<Result<IReadOnlyList<WebhookSubscription>>> Handle(
        ListWebhooksQuery request,
        CancellationToken cancellationToken)
    {
        return await _webhookService.ListByTeamAsync(request.TeamId, cancellationToken);
    }
}
