using Aegis.Domain.Services;
using MediatR;
using Aegis.Domain.Common;

namespace Aegis.Api.Features.Webhooks.List;

public record ListWebhooksQuery(Guid TeamId) : IRequest<Result<IReadOnlyList<WebhookSubscription>>>;
