using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Service for semantic caching of queries and responses
/// Uses similarity matching to find cached responses for semantically similar queries
/// </summary>
public interface ISemanticCache
{
    /// <summary>
    /// Try to get a cached response for a query
    /// </summary>
    /// <param name="request">Cache lookup request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached response if found</returns>
    Task<Result<SemanticCacheResult>> GetAsync(
        SemanticCacheRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Store a query-response pair in the cache
    /// </summary>
    /// <param name="entry">Cache entry to store</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success result</returns>
    Task<Result> SetAsync(
        SemanticCacheEntry entry,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidate cache entries matching a pattern
    /// </summary>
    /// <param name="pattern">Pattern to match (workspace, topic, etc.)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of entries invalidated</returns>
    Task<Result<int>> InvalidateAsync(
        CacheInvalidationPattern pattern,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get cache statistics
    /// </summary>
    /// <param name="workspaceId">Optional workspace filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cache statistics</returns>
    Task<Result<CacheStatistics>> GetStatisticsAsync(
        Guid? workspaceId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get cache stats summary
    /// </summary>
    Task<Result<CacheStatistics>> GetStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Evict expired cache entries
    /// </summary>
    /// <returns>Number of entries evicted</returns>
    Task<Result<int>> EvictExpiredAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Request for semantic cache lookup
/// </summary>
public record SemanticCacheRequest
{
    /// <summary>
    /// The query to find a cached response for
    /// </summary>
    public required string Query { get; init; }

    /// <summary>
    /// Pre-computed query embedding (optional)
    /// </summary>
    public float[]? QueryEmbedding { get; init; }

    /// <summary>
    /// Workspace ID for scoping
    /// </summary>
    public Guid? WorkspaceId { get; init; }

    /// <summary>
    /// Minimum similarity threshold (0.0 - 1.0)
    /// </summary>
    public double SimilarityThreshold { get; init; } = 0.95;

    /// <summary>
    /// User ID for personalized caching
    /// </summary>
    public Guid? UserId { get; init; }

    /// <summary>
    /// Additional context for matching
    /// </summary>
    public Dictionary<string, string> Context { get; init; } = new();
}

/// <summary>
/// Result of semantic cache lookup
/// </summary>
public record SemanticCacheResult
{
    /// <summary>
    /// Whether a cache hit was found
    /// </summary>
    public bool IsHit { get; init; }

    /// <summary>
    /// Cached response (if hit)
    /// </summary>
    public string? CachedResponse { get; init; }

    /// <summary>
    /// Original query that was cached
    /// </summary>
    public string? OriginalQuery { get; init; }

    /// <summary>
    /// Similarity score with the cached query
    /// </summary>
    public double SimilarityScore { get; init; }

    /// <summary>
    /// When the cached entry was created
    /// </summary>
    public DateTime? CachedAt { get; init; }

    /// <summary>
    /// Time to live remaining (in seconds)
    /// </summary>
    public int? TtlSeconds { get; init; }

    /// <summary>
    /// Cache entry ID
    /// </summary>
    public string? CacheKey { get; init; }

    /// <summary>
    /// Metadata from the cached entry
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = new();

    /// <summary>
    /// Source documents used in the cached response
    /// </summary>
    public List<string> SourceDocuments { get; init; } = new();
}

/// <summary>
/// Entry to store in semantic cache
/// </summary>
public record SemanticCacheEntry
{
    /// <summary>
    /// The query
    /// </summary>
    public required string Query { get; init; }

    /// <summary>
    /// Query embedding vector
    /// </summary>
    public required float[] QueryEmbedding { get; init; }

    /// <summary>
    /// The response to cache
    /// </summary>
    public required string Response { get; init; }

    /// <summary>
    /// Workspace ID
    /// </summary>
    public Guid? WorkspaceId { get; init; }

    /// <summary>
    /// User ID
    /// </summary>
    public Guid? UserId { get; init; }

    /// <summary>
    /// Time to live in seconds (0 = default)
    /// </summary>
    public int TtlSeconds { get; init; } = 3600; // 1 hour default

    /// <summary>
    /// Source document IDs used
    /// </summary>
    public List<Guid> SourceDocumentIds { get; init; } = new();

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = new();

    /// <summary>
    /// Tags for categorization
    /// </summary>
    public List<string> Tags { get; init; } = new();
}

/// <summary>
/// Pattern for cache invalidation
/// </summary>
public record CacheInvalidationPattern
{
    /// <summary>
    /// Workspace ID to invalidate
    /// </summary>
    public Guid? WorkspaceId { get; init; }

    /// <summary>
    /// Document IDs to invalidate caches for
    /// </summary>
    public List<Guid>? DocumentIds { get; init; }

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
/// Cache statistics
/// </summary>
public record CacheStatistics
{
    /// <summary>
    /// Total number of entries
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
    /// Average similarity score for hits
    /// </summary>
    public double AverageSimilarityScore { get; init; }

    /// <summary>
    /// Memory usage in bytes
    /// </summary>
    public long MemoryUsageBytes { get; init; }

    /// <summary>
    /// Entries by workspace
    /// </summary>
    public Dictionary<Guid, int> EntriesByWorkspace { get; init; } = new();

    /// <summary>
    /// When statistics were last reset
    /// </summary>
    public DateTime LastResetAt { get; init; }
}
