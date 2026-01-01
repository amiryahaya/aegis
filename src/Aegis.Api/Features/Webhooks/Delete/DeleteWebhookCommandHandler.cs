using Aegis.Domain.Common;
using Aegis.Domain.Services;
using MediatR;

namespace Aegis.Api.Features.Webhooks.Delete;

public class DeleteWebhookCommandHandler : IRequestHandler<DeleteWebhookCommand, Result>
{
    private readonly IWebhookService _webhookService;

    public DeleteWebhookCommandHandler(IWebhookService webhookService)
    {
        _webhookService = webhookService;
    }

    public async Task<Result> Handle(DeleteWebhookCommand request, CancellationToken cancellationToken)
    {
        return await _webhookService.DeleteAsync(request.SubscriptionId, cancellationToken);
    }
}
