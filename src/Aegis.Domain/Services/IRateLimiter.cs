using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Service for rate limiting requests per user, team, or API key
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    /// Check if a request is allowed under rate limits
    /// </summary>
    /// <param name="request">The rate limit check request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Rate limit check result</returns>
    Task<Result<RateLimitResult>> CheckAsync(
        RateLimitRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Record a request for rate limiting
    /// </summary>
    /// <param name="request">The request to record</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated rate limit status</returns>
    Task<Result<RateLimitResult>> RecordRequestAsync(
        RateLimitRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get current rate limit status for an identifier
    /// </summary>
    /// <param name="identifier">The identifier (user ID, API key, etc.)</param>
    /// <param name="limitType">Type of limit to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Current rate limit status</returns>
    Task<Result<RateLimitStatus>> GetStatusAsync(
        string identifier,
        RateLimitType limitType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reset rate limits for an identifier
    /// </summary>
    /// <param name="identifier">The identifier to reset</param>
    /// <param name="limitType">Type of limit to reset</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success result</returns>
    Task<Result> ResetAsync(
        string identifier,
        RateLimitType limitType,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Request for rate limit checking
/// </summary>
public record RateLimitRequest
{
    /// <summary>
    /// Unique identifier (user ID, API key, IP address, etc.)
    /// </summary>
    public required string Identifier { get; init; }

    /// <summary>
    /// Type of rate limit to apply
    /// </summary>
    public RateLimitType LimitType { get; init; } = RateLimitType.User;

    /// <summary>
    /// Resource being accessed
    /// </summary>
    public string Resource { get; init; } = "default";

    /// <summary>
    /// Cost of this request (for weighted limiting)
    /// </summary>
    public int Cost { get; init; } = 1;

    /// <summary>
    /// User ID (if different from identifier)
    /// </summary>
    public Guid? UserId { get; init; }

    /// <summary>
    /// Team ID for team-based limiting
    /// </summary>
    public Guid? TeamId { get; init; }

    /// <summary>
    /// API key for key-based limiting
    /// </summary>
    public string? ApiKey { get; init; }

    /// <summary>
    /// Client IP address
    /// </summary>
    public string? IpAddress { get; init; }
}

/// <summary>
/// Result of rate limit check
/// </summary>
public record RateLimitResult
{
    /// <summary>
    /// Whether the request is allowed
    /// </summary>
    public bool IsAllowed { get; init; }

    /// <summary>
    /// Current number of requests in the window
    /// </summary>
    public int CurrentCount { get; init; }

    /// <summary>
    /// Maximum allowed requests in the window
    /// </summary>
    public int MaxRequests { get; init; }

    /// <summary>
    /// Remaining requests in the window
    /// </summary>
    public int RemainingRequests { get; init; }

    /// <summary>
    /// Time until the window resets (in seconds)
    /// </summary>
    public int ResetInSeconds { get; init; }

    /// <summary>
    /// Time to wait before retry (in seconds, if blocked)
    /// </summary>
    public int RetryAfterSeconds { get; init; }

    /// <summary>
    /// Window duration in seconds
    /// </summary>
    public int WindowSeconds { get; init; }

    /// <summary>
    /// Type of limit that was checked
    /// </summary>
    public RateLimitType LimitType { get; init; }

    /// <summary>
    /// Headers to return in API response
    /// </summary>
    public Dictionary<string, string> Headers { get; init; } = new();
}

/// <summary>
/// Types of rate limits
/// </summary>
public enum RateLimitType
{
    User,
    Team,
    ApiKey,
    IpAddress,
    Global,
    Resource
}

/// <summary>
/// Current status of rate limits
/// </summary>
public record RateLimitStatus
{
    /// <summary>
    /// Identifier being tracked
    /// </summary>
    public required string Identifier { get; init; }

    /// <summary>
    /// Type of limit
    /// </summary>
    public RateLimitType LimitType { get; init; }

    /// <summary>
    /// Current count in each window
    /// </summary>
    public Dictionary<string, WindowStatus> Windows { get; init; } = new();

    /// <summary>
    /// Whether currently blocked
    /// </summary>
    public bool IsBlocked { get; init; }

    /// <summary>
    /// Reason for blocking
    /// </summary>
    public string? BlockReason { get; init; }

    /// <summary>
    /// When the block expires
    /// </summary>
    public DateTime? BlockExpiresAt { get; init; }
}

/// <summary>
/// Status of a rate limit window
/// </summary>
public record WindowStatus
{
    /// <summary>
    /// Window name (e.g., "per_second", "per_minute")
    /// </summary>
    public required string WindowName { get; init; }

    /// <summary>
    /// Current count
    /// </summary>
    public int Count { get; init; }

    /// <summary>
    /// Maximum allowed
    /// </summary>
    public int Limit { get; init; }

    /// <summary>
    /// When the window resets
    /// </summary>
    public DateTime ResetsAt { get; init; }

    /// <summary>
    /// Percentage used
    /// </summary>
    public double PercentUsed => Limit > 0 ? (double)Count / Limit * 100 : 0;
}

/// <summary>
/// Rate limit configuration
/// </summary>
public record RateLimitConfig
{
    /// <summary>
    /// Configuration name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Limits per window
    /// </summary>
    public List<WindowLimit> Limits { get; init; } = new();

    /// <summary>
    /// Whether to apply to all users
    /// </summary>
    public bool ApplyToAll { get; init; } = true;

    /// <summary>
    /// User roles this config applies to
    /// </summary>
    public List<string>? ApplicableRoles { get; init; }

    /// <summary>
    /// Resources this config applies to
    /// </summary>
    public List<string>? ApplicableResources { get; init; }
}

/// <summary>
/// Limit for a specific time window
/// </summary>
public record WindowLimit
{
    /// <summary>
    /// Window duration in seconds
    /// </summary>
    public int WindowSeconds { get; init; }

    /// <summary>
    /// Maximum requests in the window
    /// </summary>
    public int MaxRequests { get; init; }

    /// <summary>
    /// Display name for the window
    /// </summary>
    public string DisplayName => WindowSeconds switch
    {
        1 => "per second",
        60 => "per minute",
        3600 => "per hour",
        86400 => "per day",
        _ => $"per {WindowSeconds}s"
    };
}
