using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Service for publishing domain events that can trigger webhooks and other handlers
/// </summary>
public interface IEventPublisher
{
    /// <summary>
    /// Publishes an event to all registered handlers and webhooks
    /// </summary>
    Task<Result> PublishAsync(
        DomainEvent domainEvent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes multiple events
    /// </summary>
    Task<Result> PublishManyAsync(
        IEnumerable<DomainEvent> events,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers an event handler
    /// </summary>
    void Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : DomainEvent;

    /// <summary>
    /// Unregisters an event handler
    /// </summary>
    void Unsubscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : DomainEvent;
}

/// <summary>
/// Base class for domain events
/// </summary>
public abstract record DomainEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public Guid? TeamId { get; init; }
    public Guid? WorkspaceId { get; init; }
    public Guid? UserId { get; init; }
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Gets the webhook event type for this domain event
    /// </summary>
    public abstract WebhookEventType WebhookEventType { get; }
}

#region Document Events

public record DocumentUploadedEvent : DomainEvent
{
    public required Guid DocumentId { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public required long FileSize { get; init; }
    public Guid? DataSourceId { get; init; }

    public override WebhookEventType WebhookEventType => WebhookEventType.DocumentUploaded;
}

public record DocumentProcessedEvent : DomainEvent
{
    public required Guid DocumentId { get; init; }
    public required string FileName { get; init; }
    public required int ChunkCount { get; init; }
    public required int TokenCount { get; init; }
    public required TimeSpan ProcessingTime { get; init; }

    public override WebhookEventType WebhookEventType => WebhookEventType.DocumentProcessed;
}

public record DocumentProcessingFailedEvent : DomainEvent
{
    public required Guid DocumentId { get; init; }
    public required string FileName { get; init; }
    public required string ErrorMessage { get; init; }
    public string? ErrorCode { get; init; }

    public override WebhookEventType WebhookEventType => WebhookEventType.DocumentProcessingFailed;
}

public record DocumentDeletedEvent : DomainEvent
{
    public required Guid DocumentId { get; init; }
    public required string FileName { get; init; }

    public override WebhookEventType WebhookEventType => WebhookEventType.DocumentDeleted;
}

#endregion

#region Query Events

public record QueryCompletedEvent : DomainEvent
{
    public required Guid QueryId { get; init; }
    public required string Query { get; init; }
    public required int SourceCount { get; init; }
    public required TimeSpan ProcessingTime { get; init; }
    public double? ConfidenceScore { get; init; }
    public bool CacheHit { get; init; }

    public override WebhookEventType WebhookEventType => WebhookEventType.QueryCompleted;
}

public record QueryFailedEvent : DomainEvent
{
    public required Guid QueryId { get; init; }
    public required string Query { get; init; }
    public required string ErrorMessage { get; init; }
    public string? ErrorCode { get; init; }

    public override WebhookEventType WebhookEventType => WebhookEventType.QueryFailed;
}

#endregion

#region Data Source Events

public record DataSourceSyncStartedEvent : DomainEvent
{
    public required Guid DataSourceId { get; init; }
    public required string DataSourceName { get; init; }
    public required string DataSourceType { get; init; }

    public override WebhookEventType WebhookEventType => WebhookEventType.DataSourceSyncStarted;
}

public record DataSourceSyncCompletedEvent : DomainEvent
{
    public required Guid DataSourceId { get; init; }
    public required string DataSourceName { get; init; }
    public required int DocumentsProcessed { get; init; }
    public required int DocumentsAdded { get; init; }
    public required int DocumentsUpdated { get; init; }
    public required int DocumentsDeleted { get; init; }
    public required TimeSpan SyncDuration { get; init; }

    public override WebhookEventType WebhookEventType => WebhookEventType.DataSourceSyncCompleted;
}

public record DataSourceSyncFailedEvent : DomainEvent
{
    public required Guid DataSourceId { get; init; }
    public required string DataSourceName { get; init; }
    public required string ErrorMessage { get; init; }
    public string? ErrorCode { get; init; }

    public override WebhookEventType WebhookEventType => WebhookEventType.DataSourceSyncFailed;
}

#endregion

#region System Events

public record CacheClearedEvent : DomainEvent
{
    public required string CacheType { get; init; }
    public required int EntriesCleared { get; init; }

    public override WebhookEventType WebhookEventType => WebhookEventType.CacheCleared;
}

public record RateLimitExceededEvent : DomainEvent
{
    public required string ClientIdentifier { get; init; }
    public required string Endpoint { get; init; }
    public required int CurrentCount { get; init; }
    public required int Limit { get; init; }

    public override WebhookEventType WebhookEventType => WebhookEventType.RateLimitExceeded;
}

public record CircuitBreakerOpenedEvent : DomainEvent
{
    public required string OperationKey { get; init; }
    public required TimeSpan BreakDuration { get; init; }
    public required int FailureCount { get; init; }

    public override WebhookEventType WebhookEventType => WebhookEventType.CircuitBreakerOpened;
}

public record CircuitBreakerClosedEvent : DomainEvent
{
    public required string OperationKey { get; init; }

    public override WebhookEventType WebhookEventType => WebhookEventType.CircuitBreakerClosed;
}

#endregion

#region User Events

public record UserCreatedEvent : DomainEvent
{
    public required string Email { get; init; }
    public required string DisplayName { get; init; }

    public override WebhookEventType WebhookEventType => WebhookEventType.UserCreated;
}

public record UserDeletedEvent : DomainEvent
{
    public required string Email { get; init; }

    public override WebhookEventType WebhookEventType => WebhookEventType.UserDeleted;
}

public record ApiKeyCreatedEvent : DomainEvent
{
    public required string KeyName { get; init; }
    public required string KeyPrefix { get; init; }
    public DateTime? ExpiresAt { get; init; }

    public override WebhookEventType WebhookEventType => WebhookEventType.ApiKeyCreated;
}

public record ApiKeyRevokedEvent : DomainEvent
{
    public required string KeyName { get; init; }
    public required string KeyPrefix { get; init; }
    public required string Reason { get; init; }

    public override WebhookEventType WebhookEventType => WebhookEventType.ApiKeyRevoked;
}

#endregion

#region Test Event

public record TestWebhookEvent : DomainEvent
{
    public string Message { get; init; } = "This is a test webhook event";

    public override WebhookEventType WebhookEventType => WebhookEventType.Test;
}

#endregion
