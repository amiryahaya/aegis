using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Webhooks;

/// <summary>
/// In-memory implementation of webhook service
/// </summary>
public class InMemoryWebhookService : IWebhookService
{
    private readonly ConcurrentDictionary<Guid, WebhookSubscription> _subscriptions = new();
    private readonly ConcurrentDictionary<Guid, List<WebhookDelivery>> _deliveryHistory = new();
    private readonly ConcurrentDictionary<Guid, WebhookHealthTracker> _healthTrackers = new();
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<InMemoryWebhookService> _logger;
    private readonly WebhookOptions _options;

    public InMemoryWebhookService(
        IHttpClientFactory httpClientFactory,
        ILogger<InMemoryWebhookService> logger,
        WebhookOptions? options = null)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new WebhookOptions();
    }

    public Task<Result<WebhookSubscription>> RegisterAsync(
        WebhookRegistration registration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registration);

        if (string.IsNullOrWhiteSpace(registration.Url))
        {
            return Task.FromResult(Result<WebhookSubscription>.Failure(
                Error.Validation("Webhook.InvalidUrl", "Webhook URL is required")));
        }

        if (!Uri.TryCreate(registration.Url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            return Task.FromResult(Result<WebhookSubscription>.Failure(
                Error.Validation("Webhook.InvalidUrl", "Webhook URL must be a valid HTTP or HTTPS URL")));
        }

        if (registration.Events == null || registration.Events.Count == 0)
        {
            return Task.FromResult(Result<WebhookSubscription>.Failure(
                Error.Validation("Webhook.NoEvents", "At least one event type must be specified")));
        }

        var subscription = new WebhookSubscription
        {
            Id = Guid.CreateVersion7(),
            TeamId = registration.TeamId,
            Name = registration.Name,
            Url = registration.Url,
            Description = registration.Description,
            Events = registration.Events,
            SecretHash = !string.IsNullOrEmpty(registration.Secret)
                ? HashSecret(registration.Secret)
                : null,
            Headers = registration.Headers ?? new Dictionary<string, string>(),
            IsActive = registration.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _subscriptions[subscription.Id] = subscription;
        _healthTrackers[subscription.Id] = new WebhookHealthTracker();

        _logger.LogInformation(
            "Registered webhook {WebhookId} for team {TeamId} with {EventCount} events",
            subscription.Id, subscription.TeamId, subscription.Events.Count);

        return Task.FromResult(Result<WebhookSubscription>.Success(subscription));
    }

    public Task<Result<WebhookSubscription>> UpdateAsync(
        Guid subscriptionId,
        WebhookUpdate update,
        CancellationToken cancellationToken = default)
    {
        if (!_subscriptions.TryGetValue(subscriptionId, out var existing))
        {
            return Task.FromResult(Result<WebhookSubscription>.Failure(
                Error.NotFound("Webhook.NotFound", $"Webhook {subscriptionId} not found")));
        }

        var updated = existing with
        {
            Name = update.Name ?? existing.Name,
            Url = update.Url ?? existing.Url,
            Description = update.Description ?? existing.Description,
            Events = update.Events ?? existing.Events,
            SecretHash = update.Secret != null ? HashSecret(update.Secret) : existing.SecretHash,
            Headers = update.Headers ?? existing.Headers,
            IsActive = update.IsActive ?? existing.IsActive,
            UpdatedAt = DateTime.UtcNow
        };

        _subscriptions[subscriptionId] = updated;

        _logger.LogInformation("Updated webhook {WebhookId}", subscriptionId);

        return Task.FromResult(Result<WebhookSubscription>.Success(updated));
    }

    public Task<Result> DeleteAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default)
    {
        if (!_subscriptions.TryRemove(subscriptionId, out _))
        {
            return Task.FromResult(Result.Failure(
                Error.NotFound("Webhook.NotFound", $"Webhook {subscriptionId} not found")));
        }

        _deliveryHistory.TryRemove(subscriptionId, out _);
        _healthTrackers.TryRemove(subscriptionId, out _);

        _logger.LogInformation("Deleted webhook {WebhookId}", subscriptionId);

        return Task.FromResult(Result.Success());
    }

    public Task<Result<WebhookSubscription>> GetByIdAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default)
    {
        if (!_subscriptions.TryGetValue(subscriptionId, out var subscription))
        {
            return Task.FromResult(Result<WebhookSubscription>.Failure(
                Error.NotFound("Webhook.NotFound", $"Webhook {subscriptionId} not found")));
        }

        // Update health info
        if (_healthTrackers.TryGetValue(subscriptionId, out var tracker))
        {
            subscription = subscription with { Health = tracker.GetHealth() };
        }

        return Task.FromResult(Result<WebhookSubscription>.Success(subscription));
    }

    public Task<Result<IReadOnlyList<WebhookSubscription>>> ListByTeamAsync(
        Guid teamId,
        CancellationToken cancellationToken = default)
    {
        var subscriptions = _subscriptions.Values
            .Where(s => s.TeamId == teamId)
            .Select(s =>
            {
                if (_healthTrackers.TryGetValue(s.Id, out var tracker))
                {
                    return s with { Health = tracker.GetHealth() };
                }
                return s;
            })
            .ToList();

        return Task.FromResult(Result<IReadOnlyList<WebhookSubscription>>.Success(subscriptions));
    }

    public async Task<Result<WebhookDeliveryResult>> DeliverEventAsync(
        WebhookEvent webhookEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(webhookEvent);

        var matchingSubscriptions = _subscriptions.Values
            .Where(s => s.TeamId == webhookEvent.TeamId &&
                        s.IsActive &&
                        s.Events.Contains(webhookEvent.Type))
            .ToList();

        if (matchingSubscriptions.Count == 0)
        {
            return Result<WebhookDeliveryResult>.Success(new WebhookDeliveryResult
            {
                EventId = webhookEvent.Id,
                TotalSubscriptions = 0,
                SuccessfulDeliveries = 0,
                FailedDeliveries = 0,
                SkippedDeliveries = 0
            });
        }

        var deliveries = new List<WebhookDelivery>();
        var successCount = 0;
        var failedCount = 0;

        foreach (var subscription in matchingSubscriptions)
        {
            var delivery = await DeliverToSubscriptionAsync(subscription, webhookEvent, cancellationToken);
            deliveries.Add(delivery);

            if (delivery.Status == WebhookDeliveryStatus.Success)
            {
                successCount++;
            }
            else
            {
                failedCount++;
            }
        }

        return Result<WebhookDeliveryResult>.Success(new WebhookDeliveryResult
        {
            EventId = webhookEvent.Id,
            TotalSubscriptions = matchingSubscriptions.Count,
            SuccessfulDeliveries = successCount,
            FailedDeliveries = failedCount,
            SkippedDeliveries = 0,
            Deliveries = deliveries
        });
    }

    public Task<Result<IReadOnlyList<WebhookDelivery>>> GetDeliveryHistoryAsync(
        Guid subscriptionId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (!_subscriptions.ContainsKey(subscriptionId))
        {
            return Task.FromResult(Result<IReadOnlyList<WebhookDelivery>>.Failure(
                Error.NotFound("Webhook.NotFound", $"Webhook {subscriptionId} not found")));
        }

        if (_deliveryHistory.TryGetValue(subscriptionId, out var history))
        {
            var result = history
                .OrderByDescending(d => d.AttemptedAt)
                .Take(limit)
                .ToList();

            return Task.FromResult(Result<IReadOnlyList<WebhookDelivery>>.Success(result));
        }

        return Task.FromResult(Result<IReadOnlyList<WebhookDelivery>>.Success(
            Array.Empty<WebhookDelivery>()));
    }

    public async Task<Result<WebhookDelivery>> RetryDeliveryAsync(
        Guid deliveryId,
        CancellationToken cancellationToken = default)
    {
        // Find the delivery
        WebhookDelivery? originalDelivery = null;
        Guid subscriptionId = Guid.Empty;

        foreach (var (subId, history) in _deliveryHistory)
        {
            originalDelivery = history.FirstOrDefault(d => d.Id == deliveryId);
            if (originalDelivery != null)
            {
                subscriptionId = subId;
                break;
            }
        }

        if (originalDelivery == null)
        {
            return Result<WebhookDelivery>.Failure(
                Error.NotFound("Delivery.NotFound", $"Delivery {deliveryId} not found"));
        }

        if (!_subscriptions.TryGetValue(subscriptionId, out var subscription))
        {
            return Result<WebhookDelivery>.Failure(
                Error.NotFound("Webhook.NotFound", "Associated webhook not found"));
        }

        // Create a new event for retry
        var webhookEvent = new WebhookEvent
        {
            Id = originalDelivery.EventId,
            Type = originalDelivery.EventType,
            TeamId = subscription.TeamId,
            Payload = new { retryOf = deliveryId }
        };

        var delivery = await DeliverToSubscriptionAsync(
            subscription,
            webhookEvent,
            cancellationToken,
            originalDelivery.AttemptNumber + 1);

        return Result<WebhookDelivery>.Success(delivery);
    }

    public async Task<Result<WebhookDelivery>> TestAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default)
    {
        if (!_subscriptions.TryGetValue(subscriptionId, out var subscription))
        {
            return Result<WebhookDelivery>.Failure(
                Error.NotFound("Webhook.NotFound", $"Webhook {subscriptionId} not found"));
        }

        var testEvent = new WebhookEvent
        {
            Type = WebhookEventType.Test,
            TeamId = subscription.TeamId,
            Payload = new
            {
                message = "This is a test webhook event",
                timestamp = DateTime.UtcNow,
                subscriptionId = subscriptionId
            }
        };

        var delivery = await DeliverToSubscriptionAsync(subscription, testEvent, cancellationToken);

        return Result<WebhookDelivery>.Success(delivery);
    }

    public bool VerifySignature(string payload, string signature, string secret)
    {
        if (string.IsNullOrEmpty(payload) || string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(secret))
        {
            return false;
        }

        var expectedSignature = ComputeSignature(payload, secret);
        return string.Equals(signature, expectedSignature, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<WebhookDelivery> DeliverToSubscriptionAsync(
        WebhookSubscription subscription,
        WebhookEvent webhookEvent,
        CancellationToken cancellationToken,
        int attemptNumber = 1)
    {
        var stopwatch = Stopwatch.StartNew();
        WebhookDelivery delivery;

        try
        {
            var client = _httpClientFactory.CreateClient("WebhookDelivery");
            client.Timeout = _options.RequestTimeout;

            var payload = JsonSerializer.Serialize(new
            {
                id = webhookEvent.Id,
                type = webhookEvent.Type.ToString(),
                teamId = webhookEvent.TeamId,
                workspaceId = webhookEvent.WorkspaceId,
                timestamp = webhookEvent.Timestamp,
                payload = webhookEvent.Payload,
                metadata = webhookEvent.Metadata
            }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            var request = new HttpRequestMessage(HttpMethod.Post, subscription.Url)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };

            // Add custom headers
            foreach (var header in subscription.Headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            // Add signature header if secret is configured
            if (!string.IsNullOrEmpty(subscription.SecretHash))
            {
                var signature = ComputeSignature(payload, subscription.SecretHash);
                request.Headers.Add("X-Webhook-Signature", $"sha256={signature}");
            }

            request.Headers.Add("X-Webhook-Event", webhookEvent.Type.ToString());
            request.Headers.Add("X-Webhook-Delivery", webhookEvent.Id.ToString());
            request.Headers.Add("X-Webhook-Timestamp", webhookEvent.Timestamp.ToString("O"));

            var response = await client.SendAsync(request, cancellationToken);
            stopwatch.Stop();

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                delivery = new WebhookDelivery
                {
                    SubscriptionId = subscription.Id,
                    EventId = webhookEvent.Id,
                    EventType = webhookEvent.Type,
                    Url = subscription.Url,
                    Status = WebhookDeliveryStatus.Success,
                    HttpStatusCode = (int)response.StatusCode,
                    ResponseBody = responseBody.Length > 1000 ? responseBody[..1000] : responseBody,
                    AttemptNumber = attemptNumber,
                    Duration = stopwatch.Elapsed,
                    AttemptedAt = DateTime.UtcNow
                };

                UpdateHealth(subscription.Id, true, null);
                _logger.LogDebug(
                    "Successfully delivered webhook {EventType} to {Url}",
                    webhookEvent.Type, subscription.Url);
            }
            else
            {
                delivery = new WebhookDelivery
                {
                    SubscriptionId = subscription.Id,
                    EventId = webhookEvent.Id,
                    EventType = webhookEvent.Type,
                    Url = subscription.Url,
                    Status = attemptNumber >= _options.MaxRetries
                        ? WebhookDeliveryStatus.MaxRetriesExceeded
                        : WebhookDeliveryStatus.Failed,
                    HttpStatusCode = (int)response.StatusCode,
                    ResponseBody = responseBody.Length > 1000 ? responseBody[..1000] : responseBody,
                    ErrorMessage = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}",
                    AttemptNumber = attemptNumber,
                    Duration = stopwatch.Elapsed,
                    AttemptedAt = DateTime.UtcNow,
                    NextRetryAt = attemptNumber < _options.MaxRetries
                        ? DateTime.UtcNow.Add(GetRetryDelay(attemptNumber))
                        : null
                };

                UpdateHealth(subscription.Id, false, delivery.ErrorMessage);
                _logger.LogWarning(
                    "Failed to deliver webhook {EventType} to {Url}: {StatusCode}",
                    webhookEvent.Type, subscription.Url, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            delivery = new WebhookDelivery
            {
                SubscriptionId = subscription.Id,
                EventId = webhookEvent.Id,
                EventType = webhookEvent.Type,
                Url = subscription.Url,
                Status = attemptNumber >= _options.MaxRetries
                    ? WebhookDeliveryStatus.MaxRetriesExceeded
                    : WebhookDeliveryStatus.Failed,
                ErrorMessage = ex.Message,
                AttemptNumber = attemptNumber,
                Duration = stopwatch.Elapsed,
                AttemptedAt = DateTime.UtcNow,
                NextRetryAt = attemptNumber < _options.MaxRetries
                    ? DateTime.UtcNow.Add(GetRetryDelay(attemptNumber))
                    : null
            };

            UpdateHealth(subscription.Id, false, ex.Message);
            _logger.LogError(ex,
                "Exception delivering webhook {EventType} to {Url}",
                webhookEvent.Type, subscription.Url);
        }

        // Store delivery in history
        var history = _deliveryHistory.GetOrAdd(subscription.Id, _ => new List<WebhookDelivery>());
        lock (history)
        {
            history.Add(delivery);
            // Keep only last N deliveries
            while (history.Count > _options.MaxDeliveryHistoryPerWebhook)
            {
                history.RemoveAt(0);
            }
        }

        return delivery;
    }

    private void UpdateHealth(Guid subscriptionId, bool success, string? errorMessage)
    {
        var tracker = _healthTrackers.GetOrAdd(subscriptionId, _ => new WebhookHealthTracker());
        tracker.RecordDelivery(success, errorMessage);
    }

    private TimeSpan GetRetryDelay(int attemptNumber)
    {
        // Exponential backoff: 1s, 2s, 4s, 8s, etc.
        var delay = TimeSpan.FromSeconds(Math.Pow(2, attemptNumber - 1));
        return delay > _options.MaxRetryDelay ? _options.MaxRetryDelay : delay;
    }

    private static string HashSecret(string secret)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        return Convert.ToBase64String(bytes);
    }

    private static string ComputeSignature(string payload, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var hash = HMACSHA256.HashData(keyBytes, payloadBytes);
        return Convert.ToHexStringLower(hash);
    }

    private class WebhookHealthTracker
    {
        private int _successCount;
        private int _failureCount;
        private DateTime? _lastSuccessAt;
        private DateTime? _lastFailureAt;
        private string? _lastError;
        private readonly object _lock = new();

        public void RecordDelivery(bool success, string? errorMessage)
        {
            lock (_lock)
            {
                if (success)
                {
                    _successCount++;
                    _lastSuccessAt = DateTime.UtcNow;
                }
                else
                {
                    _failureCount++;
                    _lastFailureAt = DateTime.UtcNow;
                    _lastError = errorMessage;
                }
            }
        }

        public WebhookHealth GetHealth()
        {
            lock (_lock)
            {
                return new WebhookHealth
                {
                    SuccessCount = _successCount,
                    FailureCount = _failureCount,
                    LastSuccessAt = _lastSuccessAt,
                    LastFailureAt = _lastFailureAt,
                    LastError = _lastError
                };
            }
        }
    }
}

/// <summary>
/// Configuration options for webhook service
/// </summary>
public class WebhookOptions
{
    public int MaxRetries { get; set; } = 5;
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public int MaxDeliveryHistoryPerWebhook { get; set; } = 100;
}
