using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aegis.Api.Extensions;

/// <summary>
/// Extensions for health check configuration
/// </summary>
public static class HealthCheckExtensions
{
    /// <summary>
    /// Map health check endpoints with detailed response
    /// </summary>
    public static IEndpointRouteBuilder MapDetailedHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        // Liveness probe - basic check that app is running
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false, // No dependency checks for liveness
            ResponseWriter = WriteHealthResponse
        });

        // Readiness probe - checks if app can serve traffic
        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = WriteHealthResponse
        });

        // Full health check - all dependencies
        endpoints.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = WriteHealthResponse
        });

        // Startup probe - checks required for initial startup
        endpoints.MapHealthChecks("/health/startup", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("startup"),
            ResponseWriter = WriteHealthResponse
        });

        return endpoints;
    }

    /// <summary>
    /// Add detailed health checks for all dependencies
    /// </summary>
    public static IServiceCollection AddDetailedHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5434;Database=aegis;Username=postgres;Password=postgres";
        var redisConnection = configuration.GetConnectionString("Redis") ?? "localhost:6379";

        services.AddHealthChecks()
            // Database health checks
            .AddNpgSql(
                connectionString,
                name: "postgresql",
                tags: new[] { "db", "ready", "startup" },
                timeout: TimeSpan.FromSeconds(5))

            // Redis health check
            .AddRedis(
                redisConnection,
                name: "redis",
                tags: new[] { "cache", "ready" },
                timeout: TimeSpan.FromSeconds(3))

            // Memory health check
            .AddCheck<MemoryHealthCheck>(
                "memory",
                tags: new[] { "system" })

            // Disk space check
            .AddCheck<DiskSpaceHealthCheck>(
                "disk",
                tags: new[] { "system" });

        // Add Neo4j health check if configured
        var neo4jUri = configuration["Neo4j:Uri"] ?? Environment.GetEnvironmentVariable("NEO4J_URI");
        if (!string.IsNullOrWhiteSpace(neo4jUri))
        {
            services.AddHealthChecks()
                .AddCheck<Neo4jHealthCheck>(
                    "neo4j",
                    tags: new[] { "graph", "ready" });
        }

        // Add Qdrant health check if configured
        var qdrantHost = configuration["Qdrant:Host"] ?? Environment.GetEnvironmentVariable("QDRANT_HOST");
        if (!string.IsNullOrWhiteSpace(qdrantHost))
        {
            services.AddHealthChecks()
                .AddCheck<QdrantHealthCheck>(
                    "qdrant",
                    tags: new[] { "vector", "ready" });
        }

        return services;
    }

    private static async Task WriteHealthResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var response = new HealthCheckResponse
        {
            Status = report.Status.ToString(),
            Duration = report.TotalDuration,
            Checks = report.Entries.Select(e => new HealthCheckEntry
            {
                Name = e.Key,
                Status = e.Value.Status.ToString(),
                Duration = e.Value.Duration,
                Description = e.Value.Description,
                Error = e.Value.Exception?.Message,
                Data = e.Value.Data.Count > 0
                    ? e.Value.Data.ToDictionary(d => d.Key, d => d.Value?.ToString())
                    : null
            }).ToList()
        };

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        await context.Response.WriteAsJsonAsync(response, options);
    }
}

/// <summary>
/// Health check response model
/// </summary>
public class HealthCheckResponse
{
    public required string Status { get; set; }
    public TimeSpan Duration { get; set; }
    public List<HealthCheckEntry> Checks { get; set; } = new();
}

/// <summary>
/// Individual health check entry
/// </summary>
public class HealthCheckEntry
{
    public required string Name { get; set; }
    public required string Status { get; set; }
    public TimeSpan Duration { get; set; }
    public string? Description { get; set; }
    public string? Error { get; set; }
    public Dictionary<string, string?>? Data { get; set; }
}

/// <summary>
/// Memory usage health check
/// </summary>
public class MemoryHealthCheck : IHealthCheck
{
    private const long ThresholdBytes = 1024L * 1024L * 1024L; // 1 GB

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var allocatedBytes = GC.GetTotalMemory(forceFullCollection: false);
        var data = new Dictionary<string, object>
        {
            { "allocated_bytes", allocatedBytes },
            { "allocated_mb", allocatedBytes / (1024 * 1024) },
            { "threshold_mb", ThresholdBytes / (1024 * 1024) },
            { "gen0_collections", GC.CollectionCount(0) },
            { "gen1_collections", GC.CollectionCount(1) },
            { "gen2_collections", GC.CollectionCount(2) }
        };

        if (allocatedBytes >= ThresholdBytes)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                $"Memory usage is high: {allocatedBytes / (1024 * 1024)} MB",
                data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            $"Memory usage: {allocatedBytes / (1024 * 1024)} MB",
            data));
    }
}

/// <summary>
/// Disk space health check
/// </summary>
public class DiskSpaceHealthCheck : IHealthCheck
{
    private const long MinFreeSpaceBytes = 1024L * 1024L * 1024L; // 1 GB minimum

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(AppContext.BaseDirectory) ?? "/");
            var freeSpaceBytes = drive.AvailableFreeSpace;
            var totalSpaceBytes = drive.TotalSize;
            var usedPercentage = (double)(totalSpaceBytes - freeSpaceBytes) / totalSpaceBytes * 100;

            var data = new Dictionary<string, object>
            {
                { "drive", drive.Name },
                { "free_space_gb", freeSpaceBytes / (1024 * 1024 * 1024) },
                { "total_space_gb", totalSpaceBytes / (1024 * 1024 * 1024) },
                { "used_percentage", Math.Round(usedPercentage, 2) }
            };

            if (freeSpaceBytes < MinFreeSpaceBytes)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    $"Low disk space: {freeSpaceBytes / (1024 * 1024 * 1024)} GB free",
                    data: data));
            }

            if (usedPercentage > 90)
            {
                return Task.FromResult(HealthCheckResult.Degraded(
                    $"Disk usage is high: {usedPercentage:F1}%",
                    data: data));
            }

            return Task.FromResult(HealthCheckResult.Healthy(
                $"Disk space: {freeSpaceBytes / (1024 * 1024 * 1024)} GB free ({usedPercentage:F1}% used)",
                data));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"Failed to check disk space: {ex.Message}"));
        }
    }
}

/// <summary>
/// Neo4j health check
/// </summary>
public class Neo4jHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;

    public Neo4jHealthCheck(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var uri = _configuration["Neo4j:Uri"] ?? Environment.GetEnvironmentVariable("NEO4J_URI");
            var username = _configuration["Neo4j:Username"] ?? Environment.GetEnvironmentVariable("NEO4J_USERNAME");
            var password = _configuration["Neo4j:Password"] ?? Environment.GetEnvironmentVariable("NEO4J_PASSWORD");

            if (string.IsNullOrEmpty(uri))
            {
                return HealthCheckResult.Healthy("Neo4j not configured");
            }

            using var driver = Neo4j.Driver.GraphDatabase.Driver(
                uri,
                Neo4j.Driver.AuthTokens.Basic(username ?? "", password ?? ""));

            await using var session = driver.AsyncSession();
            var result = await session.RunAsync("RETURN 1 AS health");
            await result.ConsumeAsync();

            return HealthCheckResult.Healthy("Neo4j connection successful");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"Neo4j connection failed: {ex.Message}");
        }
    }
}

/// <summary>
/// Qdrant vector database health check
/// </summary>
public class QdrantHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public QdrantHealthCheck(IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var host = _configuration["Qdrant:Host"] ?? Environment.GetEnvironmentVariable("QDRANT_HOST") ?? "localhost";
            var port = _configuration["Qdrant:Port"] ?? Environment.GetEnvironmentVariable("QDRANT_PORT") ?? "6333";

            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);

            var response = await client.GetAsync($"http://{host}:{port}/readyz", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy("Qdrant connection successful");
            }

            return HealthCheckResult.Degraded($"Qdrant returned status: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"Qdrant connection failed: {ex.Message}");
        }
    }
}
