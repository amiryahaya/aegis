using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Service for managing webhooks and delivering events to external endpoints
/// </summary>
public interface IWebhookService
{
    /// <summary>
    /// Registers a new webhook subscription
    /// </summary>
    Task<Result<WebhookSubscription>> RegisterAsync(
        WebhookRegistration registration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing webhook subscription
    /// </summary>
    Task<Result<WebhookSubscription>> UpdateAsync(
        Guid subscriptionId,
        WebhookUpdate update,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a webhook subscription
    /// </summary>
    Task<Result> DeleteAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a webhook subscription by ID
    /// </summary>
    Task<Result<WebhookSubscription>> GetByIdAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists webhook subscriptions for a team
    /// </summary>
    Task<Result<IReadOnlyList<WebhookSubscription>>> ListByTeamAsync(
        Guid teamId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delivers an event to all subscribed webhooks
    /// </summary>
    Task<Result<WebhookDeliveryResult>> DeliverEventAsync(
        WebhookEvent webhookEvent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets delivery history for a webhook
    /// </summary>
    Task<Result<IReadOnlyList<WebhookDelivery>>> GetDeliveryHistoryAsync(
        Guid subscriptionId,
        int limit = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retries a failed delivery
    /// </summary>
    Task<Result<WebhookDelivery>> RetryDeliveryAsync(
        Guid deliveryId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests a webhook by sending a test event
    /// </summary>
    Task<Result<WebhookDelivery>> TestAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies webhook signature
    /// </summary>
    bool VerifySignature(string payload, string signature, string secret);
}

/// <summary>
/// Webhook subscription registration request
/// </summary>
public record WebhookRegistration
{
    public required Guid TeamId { get; init; }
    public required string Name { get; init; }
    public required string Url { get; init; }
    public string? Description { get; init; }
    public required IReadOnlyList<WebhookEventType> Events { get; init; }
    public string? Secret { get; init; }
    public Dictionary<string, string>? Headers { get; init; }
    public bool IsActive { get; init; } = true;
}

/// <summary>
/// Webhook subscription update request
/// </summary>
public record WebhookUpdate
{
    public string? Name { get; init; }
    public string? Url { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<WebhookEventType>? Events { get; init; }
    public string? Secret { get; init; }
    public Dictionary<string, string>? Headers { get; init; }
    public bool? IsActive { get; init; }
}

/// <summary>
/// Webhook subscription
/// </summary>
public record WebhookSubscription
{
    public Guid Id { get; init; }
    public Guid TeamId { get; init; }
    public required string Name { get; init; }
    public required string Url { get; init; }
    public string? Description { get; init; }
    public required IReadOnlyList<WebhookEventType> Events { get; init; }
    public string? SecretHash { get; init; }
    public Dictionary<string, string> Headers { get; init; } = new();
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public WebhookHealth Health { get; init; } = new();
}

/// <summary>
/// Webhook health status
/// </summary>
public record WebhookHealth
{
    public int SuccessCount { get; init; }
    public int FailureCount { get; init; }
    public DateTime? LastSuccessAt { get; init; }
    public DateTime? LastFailureAt { get; init; }
    public string? LastError { get; init; }
    public double SuccessRate => SuccessCount + FailureCount > 0
        ? (double)SuccessCount / (SuccessCount + FailureCount) * 100
        : 100;
}

/// <summary>
/// Event to be delivered via webhook
/// </summary>
public record WebhookEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required WebhookEventType Type { get; init; }
    public required Guid TeamId { get; init; }
    public Guid? WorkspaceId { get; init; }
    public required object Payload { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public Dictionary<string, string> Metadata { get; init; } = new();
}

/// <summary>
/// Types of events that can trigger webhooks
/// </summary>
public enum WebhookEventType
{
    // Document events
    DocumentUploaded,
    DocumentProcessed,
    DocumentProcessingFailed,
    DocumentDeleted,

    // Query events
    QueryCompleted,
    QueryFailed,

    // Data source events
    DataSourceSyncStarted,
    DataSourceSyncCompleted,
    DataSourceSyncFailed,

    // System events
    CacheCleared,
    RateLimitExceeded,
    CircuitBreakerOpened,
    CircuitBreakerClosed,

    // User events
    UserCreated,
    UserDeleted,
    ApiKeyCreated,
    ApiKeyRevoked,

    // Test event
    Test
}

/// <summary>
/// Result of delivering an event to webhooks
/// </summary>
public record WebhookDeliveryResult
{
    public Guid EventId { get; init; }
    public int TotalSubscriptions { get; init; }
    public int SuccessfulDeliveries { get; init; }
    public int FailedDeliveries { get; init; }
    public int SkippedDeliveries { get; init; }
    public IReadOnlyList<WebhookDelivery> Deliveries { get; init; } = [];
}

/// <summary>
/// Individual webhook delivery record
/// </summary>
public record WebhookDelivery
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid SubscriptionId { get; init; }
    public Guid EventId { get; init; }
    public WebhookEventType EventType { get; init; }
    public required string Url { get; init; }
    public WebhookDeliveryStatus Status { get; init; }
    public int? HttpStatusCode { get; init; }
    public string? ResponseBody { get; init; }
    public string? ErrorMessage { get; init; }
    public int AttemptNumber { get; init; }
    public TimeSpan Duration { get; init; }
    public DateTime AttemptedAt { get; init; }
    public DateTime? NextRetryAt { get; init; }
}

/// <summary>
/// Status of a webhook delivery
/// </summary>
public enum WebhookDeliveryStatus
{
    Pending,
    Success,
    Failed,
    Retrying,
    MaxRetriesExceeded,
    Skipped
}
