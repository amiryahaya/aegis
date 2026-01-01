using Aegis.Domain.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Webhooks;

/// <summary>
/// Background service for automatically retrying failed webhook deliveries
/// </summary>
public class WebhookRetryService : BackgroundService
{
    private readonly IWebhookService _webhookService;
    private readonly ILogger<WebhookRetryService> _logger;
    private readonly WebhookRetryOptions _options;

    public WebhookRetryService(
        IWebhookService webhookService,
        ILogger<WebhookRetryService> logger,
        WebhookRetryOptions? options = null)
    {
        _webhookService = webhookService ?? throw new ArgumentNullException(nameof(webhookService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new WebhookRetryOptions();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Webhook retry service starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingRetriesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing webhook retries");
            }

            await Task.Delay(_options.RetryCheckInterval, stoppingToken);
        }

        _logger.LogInformation("Webhook retry service stopping");
    }

    private async Task ProcessPendingRetriesAsync(CancellationToken cancellationToken)
    {
        // Note: In a real implementation, this would query a database for failed deliveries
        // that have NextRetryAt <= DateTime.UtcNow. For the in-memory implementation,
        // the retry logic is built into the DeliverToSubscriptionAsync method.
        // This service would be more useful with a persistent storage implementation.

        _logger.LogDebug("Checking for pending webhook retries");

        // The in-memory implementation handles retries inline, so this service
        // is primarily a placeholder for when we add database persistence.
        await Task.CompletedTask;
    }
}

/// <summary>
/// Configuration options for webhook retry service
/// </summary>
public class WebhookRetryOptions
{
    public TimeSpan RetryCheckInterval { get; set; } = TimeSpan.FromMinutes(1);
    public int MaxConcurrentRetries { get; set; } = 10;
}
