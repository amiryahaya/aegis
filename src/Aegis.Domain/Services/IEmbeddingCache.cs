using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Service for caching embeddings to reduce API calls
/// </summary>
public interface IEmbeddingCache
{
    /// <summary>
    /// Get a cached embedding for text
    /// </summary>
    /// <param name="text">The text to get embedding for</param>
    /// <param name="model">Embedding model name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached embedding if found</returns>
    Task<Result<EmbeddingCacheResult>> GetAsync(
        string text,
        string model,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get cached embeddings for multiple texts
    /// </summary>
    /// <param name="texts">Texts to get embeddings for</param>
    /// <param name="model">Embedding model name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached embeddings and cache status for each</returns>
    Task<Result<BatchEmbeddingCacheResult>> GetBatchAsync(
        List<string> texts,
        string model,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Store an embedding in the cache
    /// </summary>
    /// <param name="entry">Cache entry to store</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success result</returns>
    Task<Result> SetAsync(
        EmbeddingCacheEntry entry,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Store multiple embeddings in the cache
    /// </summary>
    /// <param name="entries">Cache entries to store</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of entries stored</returns>
    Task<Result<int>> SetBatchAsync(
        List<EmbeddingCacheEntry> entries,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidate cached embeddings
    /// </summary>
    /// <param name="pattern">Invalidation pattern</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of entries invalidated</returns>
    Task<Result<int>> InvalidateAsync(
        EmbeddingInvalidationPattern pattern,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get cache statistics
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cache statistics</returns>
    Task<Result<EmbeddingCacheStatistics>> GetStatisticsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get cache stats summary
    /// </summary>
    Task<Result<EmbeddingCacheStatistics>> GetStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Evict expired cache entries
    /// </summary>
    /// <returns>Number of entries evicted</returns>
    Task<Result<int>> EvictExpiredAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of embedding cache lookup
/// </summary>
public record EmbeddingCacheResult
{
    /// <summary>
    /// Whether a cache hit was found
    /// </summary>
    public bool IsHit { get; init; }

    /// <summary>
    /// Cached embedding vector (if hit)
    /// </summary>
    public float[]? Embedding { get; init; }

    /// <summary>
    /// Model used to generate the embedding
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// When the embedding was cached
    /// </summary>
    public DateTime? CachedAt { get; init; }

    /// <summary>
    /// Hash of the original text
    /// </summary>
    public string? TextHash { get; init; }

    /// <summary>
    /// Embedding dimension
    /// </summary>
    public int? Dimension { get; init; }
}

/// <summary>
/// Result of batch embedding cache lookup
/// </summary>
public record BatchEmbeddingCacheResult
{
    /// <summary>
    /// Results for each text (in order)
    /// </summary>
    public List<EmbeddingCacheResult> Results { get; init; } = new();

    /// <summary>
    /// Number of cache hits
    /// </summary>
    public int HitCount { get; init; }

    /// <summary>
    /// Number of cache misses
    /// </summary>
    public int MissCount { get; init; }

    /// <summary>
    /// Indices of texts that were cache misses
    /// </summary>
    public List<int> MissIndices { get; init; } = new();

    /// <summary>
    /// Texts that were cache misses
    /// </summary>
    public List<string> MissedTexts { get; init; } = new();
}

/// <summary>
/// Entry to store in embedding cache
/// </summary>
public record EmbeddingCacheEntry
{
    /// <summary>
    /// Original text
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Embedding vector
    /// </summary>
    public required float[] Embedding { get; init; }

    /// <summary>
    /// Model used to generate the embedding
    /// </summary>
    public required string Model { get; init; }

    /// <summary>
    /// Time to live in seconds (0 = default/never expires)
    /// </summary>
    public int TtlSeconds { get; init; } = 0;

    /// <summary>
    /// Source document ID (if applicable)
    /// </summary>
    public Guid? DocumentId { get; init; }

    /// <summary>
    /// Chunk index within document
    /// </summary>
    public int? ChunkIndex { get; init; }
}

/// <summary>
/// Pattern for embedding cache invalidation
/// </summary>
public record EmbeddingInvalidationPattern
{
    /// <summary>
    /// Model to invalidate embeddings for
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Document IDs to invalidate
    /// </summary>
    public List<Guid>? DocumentIds { get; init; }

    /// <summary>
    /// Specific text hashes to invalidate
    /// </summary>
    public List<string>? TextHashes { get; init; }

    /// <summary>
    /// Invalidate entries older than this
    /// </summary>
    public DateTime? OlderThan { get; init; }

    /// <summary>
    /// Invalidate all entries
    /// </summary>
    public bool InvalidateAll { get; init; } = false;
}

/// <summary>
/// Embedding cache statistics
/// </summary>
public record EmbeddingCacheStatistics
{
    /// <summary>
    /// Total cached embeddings
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
    /// Estimated memory usage in bytes
    /// </summary>
    public long MemoryUsageBytes { get; init; }

    /// <summary>
    /// Estimated API calls saved
    /// </summary>
    public long ApiCallsSaved { get; init; }

    /// <summary>
    /// Estimated cost savings (USD)
    /// </summary>
    public decimal EstimatedCostSavings { get; init; }
}
