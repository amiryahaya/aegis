using Aegis.Domain.Services;
using MediatR;
using Aegis.Domain.Common;

namespace Aegis.Api.Features.Webhooks.Register;

public record RegisterWebhookCommand(
    Guid TeamId,
    string Name,
    string Url,
    string? Description,
    IReadOnlyList<WebhookEventType> Events,
    string? Secret,
    Dictionary<string, string>? Headers,
    bool IsActive = true) : IRequest<Result<WebhookSubscription>>;
