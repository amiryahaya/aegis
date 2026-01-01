using System.Net;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace Aegis.Api.Extensions;

/// <summary>
/// Extension methods for configuring resilient HTTP clients
/// </summary>
public static class ResilientHttpClientExtensions
{
    /// <summary>
    /// Adds resilient HTTP clients for external services
    /// </summary>
    public static IServiceCollection AddResilientHttpClients(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // OpenAI API client
        services.AddHttpClient("OpenAI", client =>
            {
                client.BaseAddress = new Uri("https://api.openai.com/");
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                var apiKey = configuration["OpenAI:ApiKey"];
                if (!string.IsNullOrEmpty(apiKey))
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                }
            })
            .AddStandardResilienceHandler(ConfigureLlmResilience);

        // Cohere API client
        services.AddHttpClient("Cohere", client =>
            {
                client.BaseAddress = new Uri("https://api.cohere.ai/");
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                var apiKey = configuration["Cohere:ApiKey"];
                if (!string.IsNullOrEmpty(apiKey))
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                }
            })
            .AddStandardResilienceHandler(ConfigureLlmResilience);

        // Ollama (local LLM) client
        services.AddHttpClient("Ollama", client =>
            {
                var host = configuration["Ollama:Host"] ?? "http://localhost:11434";
                client.BaseAddress = new Uri(host);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            })
            .AddStandardResilienceHandler(ConfigureLocalLlmResilience);

        // Qdrant (vector database) client
        services.AddHttpClient("Qdrant", client =>
            {
                var host = configuration["Qdrant:Host"] ?? "localhost";
                var port = configuration["Qdrant:Port"] ?? "6333";
                client.BaseAddress = new Uri($"http://{host}:{port}");
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            })
            .AddStandardResilienceHandler(ConfigureVectorDbResilience);

        // Generic external API client
        services.AddHttpClient("ExternalApi")
            .AddStandardResilienceHandler(ConfigureExternalApiResilience);

        return services;
    }

    /// <summary>
    /// Configures resilience for LLM API clients (longer timeouts, more retries)
    /// </summary>
    private static void ConfigureLlmResilience(HttpStandardResilienceOptions options)
    {
        // Total timeout for the entire request including retries
        options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(5);

        // Retry configuration
        options.Retry.MaxRetryAttempts = 3;
        options.Retry.Delay = TimeSpan.FromSeconds(2);
        options.Retry.MaxDelay = TimeSpan.FromSeconds(60);
        options.Retry.BackoffType = DelayBackoffType.Exponential;
        options.Retry.UseJitter = true;
        options.Retry.ShouldHandle = args => ValueTask.FromResult(
            args.Outcome.Exception is not null ||
            args.Outcome.Result?.StatusCode is HttpStatusCode.TooManyRequests or
                HttpStatusCode.ServiceUnavailable or
                HttpStatusCode.GatewayTimeout or
                HttpStatusCode.RequestTimeout);

        // Circuit breaker
        options.CircuitBreaker.FailureRatio = 0.3;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
        options.CircuitBreaker.MinimumThroughput = 5;
        options.CircuitBreaker.BreakDuration = TimeSpan.FromMinutes(1);
        options.CircuitBreaker.ShouldHandle = args => ValueTask.FromResult(
            args.Outcome.Exception is not null ||
            args.Outcome.Result?.StatusCode is HttpStatusCode.ServiceUnavailable or
                HttpStatusCode.GatewayTimeout);

        // Individual attempt timeout
        options.AttemptTimeout.Timeout = TimeSpan.FromMinutes(2);
    }

    /// <summary>
    /// Configures resilience for local LLM clients (shorter breaks, more tolerance)
    /// </summary>
    private static void ConfigureLocalLlmResilience(HttpStandardResilienceOptions options)
    {
        options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(10);

        options.Retry.MaxRetryAttempts = 2;
        options.Retry.Delay = TimeSpan.FromSeconds(1);
        options.Retry.MaxDelay = TimeSpan.FromSeconds(30);
        options.Retry.BackoffType = DelayBackoffType.Exponential;
        options.Retry.UseJitter = true;

        options.CircuitBreaker.FailureRatio = 0.5;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
        options.CircuitBreaker.MinimumThroughput = 3;
        options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);

        options.AttemptTimeout.Timeout = TimeSpan.FromMinutes(5);
    }

    /// <summary>
    /// Configures resilience for vector database clients
    /// </summary>
    private static void ConfigureVectorDbResilience(HttpStandardResilienceOptions options)
    {
        options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(2);

        options.Retry.MaxRetryAttempts = 3;
        options.Retry.Delay = TimeSpan.FromMilliseconds(500);
        options.Retry.MaxDelay = TimeSpan.FromSeconds(10);
        options.Retry.BackoffType = DelayBackoffType.Exponential;
        options.Retry.UseJitter = true;

        options.CircuitBreaker.FailureRatio = 0.5;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
        options.CircuitBreaker.MinimumThroughput = 10;
        options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);

        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Configures resilience for external API clients
    /// </summary>
    private static void ConfigureExternalApiResilience(HttpStandardResilienceOptions options)
    {
        options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(1);

        options.Retry.MaxRetryAttempts = 2;
        options.Retry.Delay = TimeSpan.FromSeconds(1);
        options.Retry.MaxDelay = TimeSpan.FromSeconds(15);
        options.Retry.BackoffType = DelayBackoffType.Exponential;
        options.Retry.UseJitter = true;
        options.Retry.ShouldHandle = args => ValueTask.FromResult(
            args.Outcome.Exception is not null ||
            args.Outcome.Result?.StatusCode is HttpStatusCode.TooManyRequests or
                HttpStatusCode.ServiceUnavailable or
                HttpStatusCode.GatewayTimeout or
                HttpStatusCode.RequestTimeout or
                HttpStatusCode.InternalServerError);

        options.CircuitBreaker.FailureRatio = 0.5;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
        options.CircuitBreaker.MinimumThroughput = 5;
        options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);

        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
    }
}
