using Aegis.Api.Features.Webhooks.Delete;
using Aegis.Api.Features.Webhooks.DeliveryHistory;
using Aegis.Api.Features.Webhooks.Get;
using Aegis.Api.Features.Webhooks.List;
using Aegis.Api.Features.Webhooks.Register;
using Aegis.Api.Features.Webhooks.Test;
using Aegis.Api.Features.Webhooks.Update;
using Aegis.Domain.Services;
using Carter;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Aegis.Api.Features.Webhooks;

public class WebhookModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/webhooks")
            .WithTags("Webhooks");

        group.MapPost("/", RegisterWebhook)
            .WithSummary("Register a new webhook subscription");

        group.MapGet("/{id:guid}", GetWebhook)
            .WithSummary("Get a webhook subscription by ID");

        group.MapGet("/", ListWebhooks)
            .WithSummary("List webhook subscriptions by team");

        group.MapPut("/{id:guid}", UpdateWebhook)
            .WithSummary("Update a webhook subscription");

        group.MapDelete("/{id:guid}", DeleteWebhook)
            .WithSummary("Delete a webhook subscription");

        group.MapPost("/{id:guid}/test", TestWebhook)
            .WithSummary("Send a test event to a webhook");

        group.MapGet("/{id:guid}/deliveries", GetDeliveryHistory)
            .WithSummary("Get delivery history for a webhook");

        group.MapGet("/event-types", GetEventTypes)
            .WithSummary("Get available webhook event types");
    }

    private static async Task<Results<Created<WebhookSubscriptionResponse>, BadRequest<ProblemDetails>>>
        RegisterWebhook(
            RegisterWebhookRequest request,
            ISender sender)
    {
        var command = new RegisterWebhookCommand(
            request.TeamId,
            request.Name,
            request.Url,
            request.Description,
            request.Events,
            request.Secret,
            request.Headers,
            request.IsActive);

        var result = await sender.Send(command);

        if (result.IsFailure)
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = result.Error!.Code,
                Detail = result.Error.Message
            });
        }

        var response = MapToResponse(result.Value);
        return TypedResults.Created($"/api/webhooks/{response.Id}", response);
    }

    private static async Task<Results<Ok<WebhookSubscriptionResponse>, NotFound>> GetWebhook(
        Guid id,
        ISender sender)
    {
        var query = new GetWebhookQuery(id);
        var result = await sender.Send(query);

        if (result.IsFailure)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(MapToResponse(result.Value));
    }

    private static async Task<Ok<List<WebhookSubscriptionResponse>>> ListWebhooks(
        [FromQuery] Guid teamId,
        ISender sender)
    {
        var query = new ListWebhooksQuery(teamId);
        var result = await sender.Send(query);

        var response = result.Value.Select(MapToResponse).ToList();
        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<WebhookSubscriptionResponse>, NotFound, BadRequest<ProblemDetails>>>
        UpdateWebhook(
            Guid id,
            UpdateWebhookRequest request,
            ISender sender)
    {
        var command = new UpdateWebhookCommand(
            id,
            request.Name,
            request.Url,
            request.Description,
            request.Events,
            request.Secret,
            request.Headers,
            request.IsActive);

        var result = await sender.Send(command);

        if (result.IsFailure)
        {
            if (result.Error!.Code.Contains("NotFound"))
            {
                return TypedResults.NotFound();
            }

            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = result.Error.Code,
                Detail = result.Error.Message
            });
        }

        return TypedResults.Ok(MapToResponse(result.Value));
    }

    private static async Task<Results<NoContent, NotFound>> DeleteWebhook(
        Guid id,
        ISender sender)
    {
        var command = new DeleteWebhookCommand(id);
        var result = await sender.Send(command);

        if (result.IsFailure)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<WebhookDeliveryResponse>, NotFound, BadRequest<ProblemDetails>>>
        TestWebhook(
            Guid id,
            ISender sender)
    {
        var command = new TestWebhookCommand(id);
        var result = await sender.Send(command);

        if (result.IsFailure)
        {
            if (result.Error!.Code.Contains("NotFound"))
            {
                return TypedResults.NotFound();
            }

            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = result.Error.Code,
                Detail = result.Error.Message
            });
        }

        return TypedResults.Ok(MapToDeliveryResponse(result.Value));
    }

    private static async Task<Results<Ok<List<WebhookDeliveryResponse>>, NotFound>> GetDeliveryHistory(
        Guid id,
        [FromQuery] int limit,
        ISender sender)
    {
        var query = new GetDeliveryHistoryQuery(id, limit > 0 ? limit : 50);
        var result = await sender.Send(query);

        if (result.IsFailure)
        {
            return TypedResults.NotFound();
        }

        var response = result.Value.Select(MapToDeliveryResponse).ToList();
        return TypedResults.Ok(response);
    }

    private static Ok<List<EventTypeInfo>> GetEventTypes()
    {
        var eventTypes = Enum.GetValues<WebhookEventType>()
            .Select(e => new EventTypeInfo(
                e.ToString(),
                GetEventTypeDescription(e),
                GetEventTypeCategory(e)))
            .ToList();

        return TypedResults.Ok(eventTypes);
    }

    private static WebhookSubscriptionResponse MapToResponse(WebhookSubscription subscription)
    {
        return new WebhookSubscriptionResponse(
            subscription.Id,
            subscription.TeamId,
            subscription.Name,
            subscription.Url,
            subscription.Description,
            subscription.Events.Select(e => e.ToString()).ToList(),
            subscription.IsActive,
            subscription.CreatedAt,
            subscription.UpdatedAt,
            new WebhookHealthResponse(
                subscription.Health.SuccessCount,
                subscription.Health.FailureCount,
                subscription.Health.SuccessRate,
                subscription.Health.LastSuccessAt,
                subscription.Health.LastFailureAt,
                subscription.Health.LastError));
    }

    private static WebhookDeliveryResponse MapToDeliveryResponse(WebhookDelivery delivery)
    {
        return new WebhookDeliveryResponse(
            delivery.Id,
            delivery.SubscriptionId,
            delivery.EventId,
            delivery.EventType.ToString(),
            delivery.Status.ToString(),
            delivery.HttpStatusCode,
            delivery.ErrorMessage,
            delivery.AttemptNumber,
            delivery.Duration.TotalMilliseconds,
            delivery.AttemptedAt,
            delivery.NextRetryAt);
    }

    private static string GetEventTypeDescription(WebhookEventType eventType) => eventType switch
    {
        WebhookEventType.DocumentUploaded => "Triggered when a document is uploaded",
        WebhookEventType.DocumentProcessed => "Triggered when document processing completes successfully",
        WebhookEventType.DocumentProcessingFailed => "Triggered when document processing fails",
        WebhookEventType.DocumentDeleted => "Triggered when a document is deleted",
        WebhookEventType.QueryCompleted => "Triggered when a query completes successfully",
        WebhookEventType.QueryFailed => "Triggered when a query fails",
        WebhookEventType.DataSourceSyncStarted => "Triggered when data source synchronization starts",
        WebhookEventType.DataSourceSyncCompleted => "Triggered when data source synchronization completes",
        WebhookEventType.DataSourceSyncFailed => "Triggered when data source synchronization fails",
        WebhookEventType.CacheCleared => "Triggered when cache is cleared",
        WebhookEventType.RateLimitExceeded => "Triggered when rate limit is exceeded",
        WebhookEventType.CircuitBreakerOpened => "Triggered when circuit breaker opens",
        WebhookEventType.CircuitBreakerClosed => "Triggered when circuit breaker closes",
        WebhookEventType.UserCreated => "Triggered when a user is created",
        WebhookEventType.UserDeleted => "Triggered when a user is deleted",
        WebhookEventType.ApiKeyCreated => "Triggered when an API key is created",
        WebhookEventType.ApiKeyRevoked => "Triggered when an API key is revoked",
        WebhookEventType.Test => "Test event for webhook verification",
        _ => "Unknown event type"
    };

    private static string GetEventTypeCategory(WebhookEventType eventType) => eventType switch
    {
        WebhookEventType.DocumentUploaded or
        WebhookEventType.DocumentProcessed or
        WebhookEventType.DocumentProcessingFailed or
        WebhookEventType.DocumentDeleted => "Document",
        WebhookEventType.QueryCompleted or
        WebhookEventType.QueryFailed => "Query",
        WebhookEventType.DataSourceSyncStarted or
        WebhookEventType.DataSourceSyncCompleted or
        WebhookEventType.DataSourceSyncFailed => "DataSource",
        WebhookEventType.CacheCleared or
        WebhookEventType.RateLimitExceeded or
        WebhookEventType.CircuitBreakerOpened or
        WebhookEventType.CircuitBreakerClosed => "System",
        WebhookEventType.UserCreated or
        WebhookEventType.UserDeleted or
        WebhookEventType.ApiKeyCreated or
        WebhookEventType.ApiKeyRevoked => "User",
        WebhookEventType.Test => "Test",
        _ => "Unknown"
    };
}

#region Request/Response DTOs

public record RegisterWebhookRequest(
    Guid TeamId,
    string Name,
    string Url,
    string? Description,
    List<WebhookEventType> Events,
    string? Secret = null,
    Dictionary<string, string>? Headers = null,
    bool IsActive = true);

public record UpdateWebhookRequest(
    string? Name = null,
    string? Url = null,
    string? Description = null,
    List<WebhookEventType>? Events = null,
    string? Secret = null,
    Dictionary<string, string>? Headers = null,
    bool? IsActive = null);

public record WebhookSubscriptionResponse(
    Guid Id,
    Guid TeamId,
    string Name,
    string Url,
    string? Description,
    List<string> Events,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    WebhookHealthResponse Health);

public record WebhookHealthResponse(
    int SuccessCount,
    int FailureCount,
    double SuccessRate,
    DateTime? LastSuccessAt,
    DateTime? LastFailureAt,
    string? LastError);

public record WebhookDeliveryResponse(
    Guid Id,
    Guid SubscriptionId,
    Guid EventId,
    string EventType,
    string Status,
    int? HttpStatusCode,
    string? ErrorMessage,
    int AttemptNumber,
    double DurationMs,
    DateTime AttemptedAt,
    DateTime? NextRetryAt);

public record EventTypeInfo(string Name, string Description, string Category);

#endregion
