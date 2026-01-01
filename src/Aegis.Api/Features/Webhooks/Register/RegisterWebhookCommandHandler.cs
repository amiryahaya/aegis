using Aegis.Domain.Common;
using Aegis.Domain.Services;
using MediatR;

namespace Aegis.Api.Features.Webhooks.Register;

public class RegisterWebhookCommandHandler : IRequestHandler<RegisterWebhookCommand, Result<WebhookSubscription>>
{
    private readonly IWebhookService _webhookService;

    public RegisterWebhookCommandHandler(IWebhookService webhookService)
    {
        _webhookService = webhookService;
    }

    public async Task<Result<WebhookSubscription>> Handle(
        RegisterWebhookCommand request,
        CancellationToken cancellationToken)
    {
        var registration = new WebhookRegistration
        {
            TeamId = request.TeamId,
            Name = request.Name,
            Url = request.Url,
            Description = request.Description,
            Events = request.Events,
            Secret = request.Secret,
            Headers = request.Headers,
            IsActive = request.IsActive
        };

        return await _webhookService.RegisterAsync(registration, cancellationToken);
    }
}
