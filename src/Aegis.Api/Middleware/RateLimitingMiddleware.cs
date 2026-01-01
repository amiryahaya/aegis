using System.Net;
using Aegis.Domain.Services;
using Microsoft.Extensions.Primitives;

namespace Aegis.Api.Middleware;

/// <summary>
/// Middleware for applying rate limiting to API requests
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly RateLimitingOptions _options;

    public RateLimitingMiddleware(
        RequestDelegate next,
        ILogger<RateLimitingMiddleware> logger,
        RateLimitingOptions options)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task InvokeAsync(HttpContext context, IRateLimiter rateLimiter)
    {
        // Skip rate limiting for excluded paths
        var path = context.Request.Path.Value ?? string.Empty;
        if (_options.ExcludedPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        var clientId = GetClientIdentifier(context);
        var endpoint = GetEndpointIdentifier(context);

        var request = new RateLimitRequest
        {
            Identifier = clientId,
            Resource = endpoint,
            LimitType = GetLimitType(context),
            IpAddress = context.Connection.RemoteIpAddress?.ToString()
        };

        var result = await rateLimiter.CheckAsync(request);

        if (result.IsFailure)
        {
            _logger.LogWarning("Rate limiter error: {Error}", result.Error?.Message);
            await _next(context);
            return;
        }

        var rateLimitResult = result.Value!;

        // Add rate limit headers
        context.Response.Headers["X-RateLimit-Limit"] = rateLimitResult.MaxRequests.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = rateLimitResult.RemainingRequests.ToString();
        context.Response.Headers["X-RateLimit-Reset"] = rateLimitResult.ResetInSeconds.ToString();

        if (!rateLimitResult.IsAllowed)
        {
            _logger.LogWarning(
                "Rate limit exceeded for client {ClientId} on endpoint {Endpoint}. Retry after {RetryAfter}s",
                clientId, endpoint, rateLimitResult.RetryAfterSeconds);

            context.Response.Headers["Retry-After"] = rateLimitResult.RetryAfterSeconds.ToString();
            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;

            await context.Response.WriteAsJsonAsync(new
            {
                error = "Rate limit exceeded",
                retryAfter = rateLimitResult.RetryAfterSeconds,
                message = $"Too many requests. Please retry after {rateLimitResult.RetryAfterSeconds} seconds."
            });

            return;
        }

        // Record the request
        await rateLimiter.RecordRequestAsync(request);

        await _next(context);
    }

    private static RateLimitType GetLimitType(HttpContext context)
    {
        // Check for API key first
        if (context.Request.Headers.ContainsKey("X-Api-Key"))
        {
            return RateLimitType.ApiKey;
        }

        // Check for authenticated user
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            return RateLimitType.User;
        }

        // Fall back to IP-based limiting
        return RateLimitType.IpAddress;
    }

    private string GetClientIdentifier(HttpContext context)
    {
        // Try to get API key from header
        if (context.Request.Headers.TryGetValue("X-Api-Key", out StringValues apiKey) &&
            !string.IsNullOrEmpty(apiKey.FirstOrDefault()))
        {
            return $"apikey:{apiKey.First()}";
        }

        // Try to get user ID from claims
        var userId = context.User?.FindFirst("sub")?.Value
                     ?? context.User?.FindFirst("user_id")?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            return $"user:{userId}";
        }

        // Fall back to IP address
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Check for forwarded IP (behind proxy/load balancer)
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out StringValues forwardedFor))
        {
            var firstIp = forwardedFor.FirstOrDefault()?.Split(',').FirstOrDefault()?.Trim();
            if (!string.IsNullOrEmpty(firstIp))
            {
                ipAddress = firstIp;
            }
        }

        return $"ip:{ipAddress}";
    }

    private static string GetEndpointIdentifier(HttpContext context)
    {
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? "/";

        // Normalize path by removing IDs (for grouping similar endpoints)
        var normalizedPath = NormalizePath(path);

        return $"{method}:{normalizedPath}";
    }

    private static string NormalizePath(string path)
    {
        // Replace GUIDs and numeric IDs with placeholders
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var normalizedSegments = segments.Select(segment =>
        {
            // Check if segment is a GUID
            if (Guid.TryParse(segment, out _))
            {
                return "{id}";
            }

            // Check if segment is a number
            if (long.TryParse(segment, out _))
            {
                return "{id}";
            }

            return segment;
        });

        return "/" + string.Join("/", normalizedSegments);
    }
}

/// <summary>
/// Options for rate limiting middleware
/// </summary>
public class RateLimitingOptions
{
    /// <summary>
    /// Paths that should be excluded from rate limiting
    /// </summary>
    public List<string> ExcludedPaths { get; set; } =
    [
        "/health",
        "/metrics",
        "/swagger",
        "/hangfire"
    ];

    /// <summary>
    /// Whether to enable rate limiting
    /// </summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Extension methods for rate limiting middleware
/// </summary>
public static class RateLimitingMiddlewareExtensions
{
    /// <summary>
    /// Adds rate limiting middleware to the pipeline
    /// </summary>
    public static IApplicationBuilder UseRateLimiting(
        this IApplicationBuilder app,
        Action<RateLimitingOptions>? configureOptions = null)
    {
        var options = new RateLimitingOptions();
        configureOptions?.Invoke(options);

        if (!options.Enabled)
        {
            return app;
        }

        return app.UseMiddleware<RateLimitingMiddleware>(options);
    }
}
