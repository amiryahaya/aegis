using Aegis.Domain.Services;
using MediatR;
using Aegis.Domain.Common;

namespace Aegis.Api.Features.Webhooks.Update;

public record UpdateWebhookCommand(
    Guid SubscriptionId,
    string? Name,
    string? Url,
    string? Description,
    IReadOnlyList<WebhookEventType>? Events,
    string? Secret,
    Dictionary<string, string>? Headers,
    bool? IsActive) : IRequest<Result<WebhookSubscription>>;
