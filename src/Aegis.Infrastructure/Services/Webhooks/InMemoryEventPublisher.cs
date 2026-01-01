using System.Collections.Concurrent;
using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Webhooks;

/// <summary>
/// In-memory implementation of event publisher with webhook integration
/// </summary>
public class InMemoryEventPublisher : IEventPublisher
{
    private readonly IWebhookService _webhookService;
    private readonly ILogger<InMemoryEventPublisher> _logger;
    private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers = new();
    private readonly object _handlersLock = new();

    public InMemoryEventPublisher(
        IWebhookService webhookService,
        ILogger<InMemoryEventPublisher> logger)
    {
        _webhookService = webhookService ?? throw new ArgumentNullException(nameof(webhookService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result> PublishAsync(
        DomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        _logger.LogDebug(
            "Publishing event {EventType} with correlation {CorrelationId}",
            domainEvent.GetType().Name, domainEvent.CorrelationId);

        var errors = new List<Error>();

        // Invoke in-memory handlers
        var handlerResult = await InvokeHandlersAsync(domainEvent, cancellationToken);
        if (handlerResult.IsFailure)
        {
            errors.Add(handlerResult.Error!);
        }

        // Deliver to webhooks if team context is available
        if (domainEvent.TeamId.HasValue)
        {
            var webhookResult = await DeliverToWebhooksAsync(domainEvent, cancellationToken);
            if (webhookResult.IsFailure)
            {
                errors.Add(webhookResult.Error!);
            }
        }

        if (errors.Count > 0)
        {
            return Result.Failure(Error.Internal("Event.PublishPartialFailure",
                $"Event published with {errors.Count} error(s)"));
        }

        return Result.Success();
    }

    public async Task<Result> PublishManyAsync(
        IEnumerable<DomainEvent> events,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(events);

        var eventList = events.ToList();
        if (eventList.Count == 0)
        {
            return Result.Success();
        }

        _logger.LogDebug("Publishing {EventCount} events", eventList.Count);

        var errors = new List<Error>();

        foreach (var domainEvent in eventList)
        {
            var result = await PublishAsync(domainEvent, cancellationToken);
            if (result.IsFailure)
            {
                errors.Add(result.Error!);
            }
        }

        if (errors.Count > 0)
        {
            return Result.Failure(Error.Internal("Event.PublishManyPartialFailure",
                $"{errors.Count} of {eventList.Count} events failed to publish"));
        }

        return Result.Success();
    }

    public void Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : DomainEvent
    {
        ArgumentNullException.ThrowIfNull(handler);

        var eventType = typeof(TEvent);

        lock (_handlersLock)
        {
            var handlers = _handlers.GetOrAdd(eventType, _ => new List<Delegate>());
            handlers.Add(handler);
        }

        _logger.LogDebug("Subscribed handler for event type {EventType}", eventType.Name);
    }

    public void Unsubscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : DomainEvent
    {
        ArgumentNullException.ThrowIfNull(handler);

        var eventType = typeof(TEvent);

        lock (_handlersLock)
        {
            if (_handlers.TryGetValue(eventType, out var handlers))
            {
                handlers.Remove(handler);
                if (handlers.Count == 0)
                {
                    _handlers.TryRemove(eventType, out _);
                }
            }
        }

        _logger.LogDebug("Unsubscribed handler for event type {EventType}", eventType.Name);
    }

    private async Task<Result> InvokeHandlersAsync(
        DomainEvent domainEvent,
        CancellationToken cancellationToken)
    {
        var eventType = domainEvent.GetType();
        List<Delegate>? handlers;

        lock (_handlersLock)
        {
            if (!_handlers.TryGetValue(eventType, out handlers) || handlers.Count == 0)
            {
                return Result.Success();
            }
            // Create a copy to avoid holding the lock during invocation
            handlers = handlers.ToList();
        }

        var errors = new List<Error>();

        foreach (var handler in handlers)
        {
            try
            {
                // Use reflection to invoke the handler with the correct event type
                var invokeMethod = handler.GetType().GetMethod("Invoke");
                if (invokeMethod != null)
                {
                    var task = invokeMethod.Invoke(handler, [domainEvent, cancellationToken]) as Task;
                    if (task != null)
                    {
                        await task;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Handler failed for event {EventType}: {Error}",
                    eventType.Name, ex.Message);

                errors.Add(Error.Internal(
                    "Event.HandlerFailed",
                    $"Handler failed: {ex.Message}"));
            }
        }

        if (errors.Count > 0)
        {
            return Result.Failure(Error.Internal("Event.HandlersPartialFailure",
                $"{errors.Count} handler(s) failed"));
        }

        return Result.Success();
    }

    private async Task<Result> DeliverToWebhooksAsync(
        DomainEvent domainEvent,
        CancellationToken cancellationToken)
    {
        try
        {
            var webhookEvent = new WebhookEvent
            {
                Type = domainEvent.WebhookEventType,
                TeamId = domainEvent.TeamId!.Value,
                WorkspaceId = domainEvent.WorkspaceId,
                Payload = CreatePayload(domainEvent),
                Metadata = new Dictionary<string, string>
                {
                    ["eventId"] = domainEvent.Id.ToString(),
                    ["eventType"] = domainEvent.GetType().Name,
                    ["correlationId"] = domainEvent.CorrelationId ?? string.Empty
                }
            };

            var result = await _webhookService.DeliverEventAsync(webhookEvent, cancellationToken);

            if (result.IsSuccess && result.Value.FailedDeliveries > 0)
            {
                _logger.LogWarning(
                    "Event {EventType} delivered to {Success}/{Total} webhooks",
                    domainEvent.GetType().Name,
                    result.Value.SuccessfulDeliveries,
                    result.Value.TotalSubscriptions);
            }

            return result.IsSuccess
                ? Result.Success()
                : Result.Failure(result.Error!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to deliver event {EventType} to webhooks",
                domainEvent.GetType().Name);

            return Result.Failure(Error.Internal(
                "Event.WebhookDeliveryFailed",
                $"Webhook delivery failed: {ex.Message}"));
        }
    }

    private static object CreatePayload(DomainEvent domainEvent)
    {
        // Create payload based on event type
        return domainEvent switch
        {
            DocumentUploadedEvent e => new
            {
                documentId = e.DocumentId,
                fileName = e.FileName,
                contentType = e.ContentType,
                fileSize = e.FileSize,
                dataSourceId = e.DataSourceId
            },
            DocumentProcessedEvent e => new
            {
                documentId = e.DocumentId,
                fileName = e.FileName,
                chunkCount = e.ChunkCount,
                tokenCount = e.TokenCount,
                processingTimeMs = e.ProcessingTime.TotalMilliseconds
            },
            DocumentProcessingFailedEvent e => new
            {
                documentId = e.DocumentId,
                fileName = e.FileName,
                errorMessage = e.ErrorMessage,
                errorCode = e.ErrorCode
            },
            DocumentDeletedEvent e => new
            {
                documentId = e.DocumentId,
                fileName = e.FileName
            },
            QueryCompletedEvent e => new
            {
                queryId = e.QueryId,
                query = e.Query,
                sourceCount = e.SourceCount,
                processingTimeMs = e.ProcessingTime.TotalMilliseconds,
                confidenceScore = e.ConfidenceScore,
                cacheHit = e.CacheHit
            },
            QueryFailedEvent e => new
            {
                queryId = e.QueryId,
                query = e.Query,
                errorMessage = e.ErrorMessage,
                errorCode = e.ErrorCode
            },
            DataSourceSyncStartedEvent e => new
            {
                dataSourceId = e.DataSourceId,
                dataSourceName = e.DataSourceName,
                dataSourceType = e.DataSourceType
            },
            DataSourceSyncCompletedEvent e => new
            {
                dataSourceId = e.DataSourceId,
                dataSourceName = e.DataSourceName,
                documentsProcessed = e.DocumentsProcessed,
                documentsAdded = e.DocumentsAdded,
                documentsUpdated = e.DocumentsUpdated,
                documentsDeleted = e.DocumentsDeleted,
                syncDurationMs = e.SyncDuration.TotalMilliseconds
            },
            DataSourceSyncFailedEvent e => new
            {
                dataSourceId = e.DataSourceId,
                dataSourceName = e.DataSourceName,
                errorMessage = e.ErrorMessage,
                errorCode = e.ErrorCode
            },
            CacheClearedEvent e => new
            {
                cacheType = e.CacheType,
                entriesCleared = e.EntriesCleared
            },
            RateLimitExceededEvent e => new
            {
                clientIdentifier = e.ClientIdentifier,
                endpoint = e.Endpoint,
                currentCount = e.CurrentCount,
                limit = e.Limit
            },
            CircuitBreakerOpenedEvent e => new
            {
                operationKey = e.OperationKey,
                breakDurationMs = e.BreakDuration.TotalMilliseconds,
                failureCount = e.FailureCount
            },
            CircuitBreakerClosedEvent e => new
            {
                operationKey = e.OperationKey
            },
            UserCreatedEvent e => new
            {
                email = e.Email,
                displayName = e.DisplayName
            },
            UserDeletedEvent e => new
            {
                email = e.Email
            },
            ApiKeyCreatedEvent e => new
            {
                keyName = e.KeyName,
                keyPrefix = e.KeyPrefix,
                expiresAt = e.ExpiresAt
            },
            ApiKeyRevokedEvent e => new
            {
                keyName = e.KeyName,
                keyPrefix = e.KeyPrefix,
                reason = e.Reason
            },
            TestWebhookEvent e => new
            {
                message = e.Message
            },
            _ => new
            {
                eventType = domainEvent.GetType().Name,
                occurredAt = domainEvent.OccurredAt
            }
        };
    }
}
