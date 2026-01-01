using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Prometheus;

namespace Aegis.Api.Extensions;

/// <summary>
/// Extensions for observability (metrics, tracing, logging)
/// </summary>
public static class ObservabilityExtensions
{
    private const string ServiceName = "aegis-api";
    private const string ServiceVersion = "1.0.0";

    /// <summary>
    /// Add observability services (metrics, tracing)
    /// </summary>
    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Add Prometheus metrics
        services.AddPrometheusMetrics();

        // Add OpenTelemetry tracing
        services.AddOpenTelemetryTracing(configuration);

        // Add custom metrics
        services.AddSingleton<AegisMetrics>();

        return services;
    }

    /// <summary>
    /// Configure Prometheus metrics collection
    /// </summary>
    private static IServiceCollection AddPrometheusMetrics(this IServiceCollection services)
    {
        // Enable default .NET metrics
        Metrics.SuppressDefaultMetrics();

        return services;
    }

    /// <summary>
    /// Configure OpenTelemetry distributed tracing
    /// </summary>
    private static IServiceCollection AddOpenTelemetryTracing(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var otlpEndpoint = configuration["OpenTelemetry:OtlpEndpoint"]
            ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName: ServiceName,
                    serviceVersion: ServiceVersion,
                    serviceInstanceId: Environment.MachineName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.Filter = httpContext =>
                        {
                            // Skip health checks and metrics endpoints
                            var path = httpContext.Request.Path.Value ?? "";
                            return !path.StartsWith("/health") &&
                                   !path.StartsWith("/metrics") &&
                                   !path.StartsWith("/swagger");
                        };
                    })
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                    })
                    .AddSource(ServiceName);

                // Add OTLP exporter if endpoint is configured
                if (!string.IsNullOrEmpty(otlpEndpoint))
                {
                    tracing.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otlpEndpoint);
                    });
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddProcessInstrumentation();

                // Add OTLP exporter if endpoint is configured
                if (!string.IsNullOrEmpty(otlpEndpoint))
                {
                    metrics.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otlpEndpoint);
                    });
                }
            });

        return services;
    }

    /// <summary>
    /// Map observability endpoints (metrics, health)
    /// </summary>
    public static IApplicationBuilder UseObservability(this IApplicationBuilder app)
    {
        // Prometheus metrics endpoint
        app.UseMetricServer("/metrics");

        // HTTP request metrics
        app.UseHttpMetrics(options =>
        {
            options.AddCustomLabel("service", context => ServiceName);
        });

        return app;
    }
}

/// <summary>
/// Custom application metrics
/// </summary>
public class AegisMetrics
{
    // Query metrics
    private readonly Counter _queryCounter;
    private readonly Histogram _queryDuration;
    private readonly Gauge _activeQueries;

    // Document metrics
    private readonly Counter _documentsIngested;
    private readonly Counter _documentsFailed;
    private readonly Gauge _documentsTotal;

    // Cache metrics
    private readonly Counter _cacheHits;
    private readonly Counter _cacheMisses;

    // LLM metrics
    private readonly Counter _llmRequests;
    private readonly Histogram _llmLatency;
    private readonly Counter _llmTokensUsed;

    // Authentication metrics
    private readonly Counter _authAttempts;
    private readonly Counter _authFailures;

    public AegisMetrics()
    {
        // Query metrics
        _queryCounter = Metrics.CreateCounter(
            "aegis_queries_total",
            "Total number of queries processed",
            new CounterConfiguration
            {
                LabelNames = new[] { "workspace", "status" }
            });

        _queryDuration = Metrics.CreateHistogram(
            "aegis_query_duration_seconds",
            "Query processing duration in seconds",
            new Prometheus.HistogramConfiguration
            {
                LabelNames = new[] { "workspace" },
                Buckets = new[] { 0.1, 0.25, 0.5, 1.0, 2.5, 5.0, 10.0 }
            });

        _activeQueries = Metrics.CreateGauge(
            "aegis_active_queries",
            "Number of currently active queries");

        // Document metrics
        _documentsIngested = Metrics.CreateCounter(
            "aegis_documents_ingested_total",
            "Total number of documents ingested",
            new CounterConfiguration
            {
                LabelNames = new[] { "workspace", "type" }
            });

        _documentsFailed = Metrics.CreateCounter(
            "aegis_documents_failed_total",
            "Total number of document ingestion failures",
            new CounterConfiguration
            {
                LabelNames = new[] { "workspace", "reason" }
            });

        _documentsTotal = Metrics.CreateGauge(
            "aegis_documents_total",
            "Total number of documents in the system",
            new GaugeConfiguration
            {
                LabelNames = new[] { "workspace" }
            });

        // Cache metrics
        _cacheHits = Metrics.CreateCounter(
            "aegis_cache_hits_total",
            "Total number of cache hits",
            new CounterConfiguration
            {
                LabelNames = new[] { "cache_type" }
            });

        _cacheMisses = Metrics.CreateCounter(
            "aegis_cache_misses_total",
            "Total number of cache misses",
            new CounterConfiguration
            {
                LabelNames = new[] { "cache_type" }
            });

        // LLM metrics
        _llmRequests = Metrics.CreateCounter(
            "aegis_llm_requests_total",
            "Total number of LLM requests",
            new CounterConfiguration
            {
                LabelNames = new[] { "model", "status" }
            });

        _llmLatency = Metrics.CreateHistogram(
            "aegis_llm_latency_seconds",
            "LLM request latency in seconds",
            new Prometheus.HistogramConfiguration
            {
                LabelNames = new[] { "model" },
                Buckets = new[] { 0.5, 1.0, 2.0, 5.0, 10.0, 30.0, 60.0 }
            });

        _llmTokensUsed = Metrics.CreateCounter(
            "aegis_llm_tokens_total",
            "Total number of LLM tokens used",
            new CounterConfiguration
            {
                LabelNames = new[] { "model", "type" }
            });

        // Authentication metrics
        _authAttempts = Metrics.CreateCounter(
            "aegis_auth_attempts_total",
            "Total number of authentication attempts",
            new CounterConfiguration
            {
                LabelNames = new[] { "method" }
            });

        _authFailures = Metrics.CreateCounter(
            "aegis_auth_failures_total",
            "Total number of authentication failures",
            new CounterConfiguration
            {
                LabelNames = new[] { "method", "reason" }
            });
    }

    // Query methods
    public void RecordQuery(string workspace, bool success)
    {
        _queryCounter.WithLabels(workspace, success ? "success" : "failure").Inc();
    }

    public IDisposable TrackQueryDuration(string workspace)
    {
        _activeQueries.Inc();
        return _queryDuration.WithLabels(workspace).NewTimer();
    }

    public void QueryCompleted()
    {
        _activeQueries.Dec();
    }

    // Document methods
    public void RecordDocumentIngested(string workspace, string documentType)
    {
        _documentsIngested.WithLabels(workspace, documentType).Inc();
    }

    public void RecordDocumentFailed(string workspace, string reason)
    {
        _documentsFailed.WithLabels(workspace, reason).Inc();
    }

    public void SetDocumentCount(string workspace, double count)
    {
        _documentsTotal.WithLabels(workspace).Set(count);
    }

    // Cache methods
    public void RecordCacheHit(string cacheType)
    {
        _cacheHits.WithLabels(cacheType).Inc();
    }

    public void RecordCacheMiss(string cacheType)
    {
        _cacheMisses.WithLabels(cacheType).Inc();
    }

    // LLM methods
    public void RecordLLMRequest(string model, bool success)
    {
        _llmRequests.WithLabels(model, success ? "success" : "failure").Inc();
    }

    public IDisposable TrackLLMLatency(string model)
    {
        return _llmLatency.WithLabels(model).NewTimer();
    }

    public void RecordTokenUsage(string model, int promptTokens, int completionTokens)
    {
        _llmTokensUsed.WithLabels(model, "prompt").Inc(promptTokens);
        _llmTokensUsed.WithLabels(model, "completion").Inc(completionTokens);
    }

    // Auth methods
    public void RecordAuthAttempt(string method)
    {
        _authAttempts.WithLabels(method).Inc();
    }

    public void RecordAuthFailure(string method, string reason)
    {
        _authFailures.WithLabels(method, reason).Inc();
    }
}
