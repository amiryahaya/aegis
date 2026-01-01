using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Service for caching LLM responses to improve latency and reduce costs
/// </summary>
public interface IResponseCache
{
    /// <summary>
    /// Get a cached LLM response
    /// </summary>
    /// <param name="request">Cache lookup request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached response if found</returns>
    Task<Result<ResponseCacheResult>> GetAsync(
        ResponseCacheRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Store an LLM response in the cache
    /// </summary>
    /// <param name="entry">Cache entry to store</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success result</returns>
    Task<Result> SetAsync(
        ResponseCacheEntry entry,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidate cached responses
    /// </summary>
    /// <param name="pattern">Invalidation pattern</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of entries invalidated</returns>
    Task<Result<int>> InvalidateAsync(
        ResponseInvalidationPattern pattern,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get cache statistics
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cache statistics</returns>
    Task<Result<ResponseCacheStatistics>> GetStatisticsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get cache stats summary
    /// </summary>
    Task<Result<ResponseCacheStatistics>> GetStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Evict expired cache entries
    /// </summary>
    /// <returns>Number of entries evicted</returns>
    Task<Result<int>> EvictExpiredAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Request for response cache lookup
/// </summary>
public record ResponseCacheRequest
{
    /// <summary>
    /// The prompt/input to look up
    /// </summary>
    public required string Prompt { get; init; }

    /// <summary>
    /// System prompt used (affects cache key)
    /// </summary>
    public string? SystemPrompt { get; init; }

    /// <summary>
    /// Model name
    /// </summary>
    public required string Model { get; init; }

    /// <summary>
    /// Temperature setting (affects cache key)
    /// </summary>
    public double Temperature { get; init; } = 0.7;

    /// <summary>
    /// Additional context that affects the response
    /// </summary>
    public string? Context { get; init; }

    /// <summary>
    /// Workspace ID for scoping
    /// </summary>
    public Guid? WorkspaceId { get; init; }

    /// <summary>
    /// Whether to use fuzzy matching for prompts
    /// </summary>
    public bool UseFuzzyMatch { get; init; } = false;

    /// <summary>
    /// Fuzzy match threshold (0.0 - 1.0)
    /// </summary>
    public double FuzzyMatchThreshold { get; init; } = 0.98;
}

/// <summary>
/// Result of response cache lookup
/// </summary>
public record ResponseCacheResult
{
    /// <summary>
    /// Whether a cache hit was found
    /// </summary>
    public bool IsHit { get; init; }

    /// <summary>
    /// Cached response (if hit)
    /// </summary>
    public string? Response { get; init; }

    /// <summary>
    /// Model used to generate the response
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Token count of cached response
    /// </summary>
    public int? TokenCount { get; init; }

    /// <summary>
    /// When the response was cached
    /// </summary>
    public DateTime? CachedAt { get; init; }

    /// <summary>
    /// Time to live remaining (in seconds)
    /// </summary>
    public int? TtlSeconds { get; init; }

    /// <summary>
    /// Cache key used
    /// </summary>
    public string? CacheKey { get; init; }

    /// <summary>
    /// Whether this was a fuzzy match
    /// </summary>
    public bool IsFuzzyMatch { get; init; }

    /// <summary>
    /// Match score (1.0 for exact, less for fuzzy)
    /// </summary>
    public double MatchScore { get; init; }

    /// <summary>
    /// Original latency when response was generated (ms)
    /// </summary>
    public int? OriginalLatencyMs { get; init; }

    /// <summary>
    /// Estimated cost savings from cache hit
    /// </summary>
    public decimal? CostSavings { get; init; }
}

/// <summary>
/// Entry to store in response cache
/// </summary>
public record ResponseCacheEntry
{
    /// <summary>
    /// The prompt/input
    /// </summary>
    public required string Prompt { get; init; }

    /// <summary>
    /// System prompt used
    /// </summary>
    public string? SystemPrompt { get; init; }

    /// <summary>
    /// The LLM response
    /// </summary>
    public required string Response { get; init; }

    /// <summary>
    /// Model name
    /// </summary>
    public required string Model { get; init; }

    /// <summary>
    /// Temperature used
    /// </summary>
    public double Temperature { get; init; }

    /// <summary>
    /// Additional context
    /// </summary>
    public string? Context { get; init; }

    /// <summary>
    /// Workspace ID
    /// </summary>
    public Guid? WorkspaceId { get; init; }

    /// <summary>
    /// Time to live in seconds
    /// </summary>
    public int TtlSeconds { get; init; } = 3600;

    /// <summary>
    /// Input token count
    /// </summary>
    public int InputTokens { get; init; }

    /// <summary>
    /// Output token count
    /// </summary>
    public int OutputTokens { get; init; }

    /// <summary>
    /// Generation latency in milliseconds
    /// </summary>
    public int LatencyMs { get; init; }

    /// <summary>
    /// Estimated cost of this response
    /// </summary>
    public decimal EstimatedCost { get; init; }

    /// <summary>
    /// Tags for categorization
    /// </summary>
    public List<string> Tags { get; init; } = new();
}

/// <summary>
/// Pattern for response cache invalidation
/// </summary>
public record ResponseInvalidationPattern
{
    /// <summary>
    /// Model to invalidate responses for
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Workspace ID to invalidate
    /// </summary>
    public Guid? WorkspaceId { get; init; }

    /// <summary>
    /// Tags to match for invalidation
    /// </summary>
    public List<string>? Tags { get; init; }

    /// <summary>
    /// Invalidate entries older than this
    /// </summary>
    public DateTime? OlderThan { get; init; }

    /// <summary>
    /// Specific cache keys to invalidate
    /// </summary>
    public List<string>? CacheKeys { get; init; }

    /// <summary>
    /// Invalidate all entries
    /// </summary>
    public bool InvalidateAll { get; init; } = false;
}

/// <summary>
/// Response cache statistics
/// </summary>
public record ResponseCacheStatistics
{
    /// <summary>
    /// Total cached responses
    /// </summary>
    public long TotalEntries { get; init; }

    /// <summary>
    /// Total cache hits
    /// </summary>
    public long TotalHits { get; init; }

    /// <summary>
    /// Total cache misses
    /// </summary>
    public long TotalMisses { get; init; }

    /// <summary>
    /// Cache hit rate
    /// </summary>
    public double HitRate => TotalHits + TotalMisses > 0
        ? (double)TotalHits / (TotalHits + TotalMisses)
        : 0;

    /// <summary>
    /// Entries by model
    /// </summary>
    public Dictionary<string, long> EntriesByModel { get; init; } = new();

    /// <summary>
    /// Total tokens cached
    /// </summary>
    public long TotalTokensCached { get; init; }

    /// <summary>
    /// Total API calls saved
    /// </summary>
    public long ApiCallsSaved { get; init; }

    /// <summary>
    /// Total latency saved (ms)
    /// </summary>
    public long TotalLatencySavedMs { get; init; }

    /// <summary>
    /// Average latency saved per hit (ms)
    /// </summary>
    public double AverageLatencySavedMs => TotalHits > 0
        ? (double)TotalLatencySavedMs / TotalHits
        : 0;

    /// <summary>
    /// Estimated total cost savings (USD)
    /// </summary>
    public decimal TotalCostSavings { get; init; }

    /// <summary>
    /// Memory usage in bytes
    /// </summary>
    public long MemoryUsageBytes { get; init; }
}
