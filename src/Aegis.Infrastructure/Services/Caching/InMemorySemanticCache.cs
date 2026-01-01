using System.Collections.Concurrent;
using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Caching;

/// <summary>
/// In-memory semantic cache implementation
/// </summary>
public class InMemorySemanticCache : ISemanticCache
{
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<InMemorySemanticCache> _logger;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private long _hits;
    private long _misses;

    public InMemorySemanticCache(
        IEmbeddingService embeddingService,
        ILogger<InMemorySemanticCache> logger)
    {
        _embeddingService = embeddingService;
        _logger = logger;
    }

    public async Task<Result<SemanticCacheResult>> GetAsync(
        SemanticCacheRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                return Result<SemanticCacheResult>.Failure(
                    Error.Validation("SemanticCache.EmptyQuery", "Query cannot be empty"));
            }

            // Get or generate query embedding
            var queryEmbedding = request.QueryEmbedding;
            if (queryEmbedding == null)
            {
                var embeddingResult = await _embeddingService.GenerateEmbeddingAsync(request.Query, cancellationToken);
                if (embeddingResult.IsFailure)
                {
                    Interlocked.Increment(ref _misses);
                    return Result<SemanticCacheResult>.Success(new SemanticCacheResult { IsHit = false });
                }
                queryEmbedding = embeddingResult.Value;
            }

            // Search for similar entries
            var bestMatch = FindBestMatch(queryEmbedding, request.WorkspaceId, request.SimilarityThreshold);

            if (bestMatch == null)
            {
                Interlocked.Increment(ref _misses);
                return Result<SemanticCacheResult>.Success(new SemanticCacheResult { IsHit = false });
            }

            Interlocked.Increment(ref _hits);

            var remainingTtl = bestMatch.ExpiresAt.HasValue
                ? (int)(bestMatch.ExpiresAt.Value - DateTime.UtcNow).TotalSeconds
                : (int?)null;

            return Result<SemanticCacheResult>.Success(new SemanticCacheResult
            {
                IsHit = true,
                CachedResponse = bestMatch.Response,
                OriginalQuery = bestMatch.Query,
                SimilarityScore = bestMatch.SimilarityScore,
                CachedAt = bestMatch.CreatedAt,
                TtlSeconds = remainingTtl,
                CacheKey = bestMatch.Key,
                Metadata = bestMatch.Metadata,
                SourceDocuments = bestMatch.SourceDocumentIds.Select(id => id.ToString()).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting from semantic cache");
            return Result<SemanticCacheResult>.Failure(
                Error.Internal("SemanticCache.Error", ex.Message));
        }
    }

    public async Task<Result> SetAsync(
        SemanticCacheEntry entry,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var key = GenerateKey(entry.Query, entry.WorkspaceId);
            var expiresAt = entry.TtlSeconds > 0
                ? DateTime.UtcNow.AddSeconds(entry.TtlSeconds)
                : (DateTime?)null;

            var cacheEntry = new CacheEntry
            {
                Key = key,
                Query = entry.Query,
                QueryEmbedding = entry.QueryEmbedding,
                Response = entry.Response,
                WorkspaceId = entry.WorkspaceId,
                UserId = entry.UserId,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = expiresAt,
                SourceDocumentIds = entry.SourceDocumentIds.ToList(),
                Metadata = new Dictionary<string, string>(entry.Metadata),
                Tags = entry.Tags.ToList()
            };

            _cache[key] = cacheEntry;

            _logger.LogDebug("Cached semantic response for query: {Query}", entry.Query.Substring(0, Math.Min(50, entry.Query.Length)));

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting semantic cache");
            return Result.Failure(Error.Internal("SemanticCache.Error", ex.Message));
        }
    }

    public async Task<Result<int>> InvalidateAsync(
        CacheInvalidationPattern pattern,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var keysToRemove = new List<string>();

            foreach (var (key, entry) in _cache)
            {
                if (ShouldInvalidate(entry, pattern))
                {
                    keysToRemove.Add(key);
                }
            }

            foreach (var key in keysToRemove)
            {
                _cache.TryRemove(key, out _);
            }

            _logger.LogInformation("Invalidated {Count} semantic cache entries", keysToRemove.Count);

            return Result<int>.Success(keysToRemove.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating semantic cache");
            return Result<int>.Failure(Error.Internal("SemanticCache.Error", ex.Message));
        }
    }

    public async Task<Result<CacheStatistics>> GetStatisticsAsync(
        Guid? workspaceId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entries = workspaceId.HasValue
                ? _cache.Values.Where(e => e.WorkspaceId == workspaceId.Value).ToList()
                : _cache.Values.ToList();

            var entriesByWorkspace = entries
                .Where(e => e.WorkspaceId.HasValue)
                .GroupBy(e => e.WorkspaceId!.Value)
                .ToDictionary(g => g.Key, g => g.Count());

            return Result<CacheStatistics>.Success(new CacheStatistics
            {
                TotalEntries = entries.Count,
                TotalHits = _hits,
                TotalMisses = _misses,
                AverageSimilarityScore = entries.Count > 0 ? entries.Average(e => e.SimilarityScore) : 0,
                MemoryUsageBytes = EstimateMemoryUsage(entries),
                EntriesByWorkspace = entriesByWorkspace,
                LastResetAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cache statistics");
            return Result<CacheStatistics>.Failure(
                Error.Internal("SemanticCache.Error", ex.Message));
        }
    }

    public Task<Result<CacheStatistics>> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        return GetStatisticsAsync(null, cancellationToken);
    }

    public Task<Result<int>> EvictExpiredAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var keysToRemove = new List<string>();
            var now = DateTime.UtcNow;

            foreach (var (key, entry) in _cache)
            {
                if (entry.ExpiresAt.HasValue && entry.ExpiresAt.Value < now)
                {
                    keysToRemove.Add(key);
                }
            }

            foreach (var key in keysToRemove)
            {
                _cache.TryRemove(key, out _);
            }

            _logger.LogDebug("Evicted {Count} expired semantic cache entries", keysToRemove.Count);

            return Task.FromResult(Result<int>.Success(keysToRemove.Count));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evicting expired cache entries");
            return Task.FromResult(Result<int>.Failure(Error.Internal("SemanticCache.Error", ex.Message)));
        }
    }

    private CacheEntry? FindBestMatch(float[] queryEmbedding, Guid? workspaceId, double threshold)
    {
        CacheEntry? bestMatch = null;
        var bestScore = threshold;

        foreach (var entry in _cache.Values)
        {
            // Skip if workspace doesn't match
            if (workspaceId.HasValue && entry.WorkspaceId != workspaceId)
                continue;

            // Skip expired entries
            if (entry.ExpiresAt.HasValue && entry.ExpiresAt.Value < DateTime.UtcNow)
                continue;

            var similarity = CosineSimilarity(queryEmbedding, entry.QueryEmbedding);

            if (similarity >= bestScore)
            {
                bestScore = similarity;
                bestMatch = entry;
                entry.SimilarityScore = similarity;
            }
        }

        return bestMatch;
    }

    private double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;

        double dotProduct = 0;
        double magnitudeA = 0;
        double magnitudeB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }

        if (magnitudeA == 0 || magnitudeB == 0) return 0;

        return dotProduct / (Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB));
    }

    private bool ShouldInvalidate(CacheEntry entry, CacheInvalidationPattern pattern)
    {
        if (pattern.InvalidateAll) return true;

        if (pattern.WorkspaceId.HasValue && entry.WorkspaceId == pattern.WorkspaceId)
            return true;

        if (pattern.CacheKeys?.Contains(entry.Key) == true)
            return true;

        if (pattern.DocumentIds?.Any(id => entry.SourceDocumentIds.Contains(id)) == true)
            return true;

        if (pattern.Tags?.Any(tag => entry.Tags.Contains(tag)) == true)
            return true;

        if (pattern.OlderThan.HasValue && entry.CreatedAt < pattern.OlderThan)
            return true;

        return false;
    }

    private string GenerateKey(string query, Guid? workspaceId)
    {
        var hash = query.GetHashCode();
        return workspaceId.HasValue
            ? $"{workspaceId.Value}:{hash}"
            : $"global:{hash}";
    }

    private long EstimateMemoryUsage(List<CacheEntry> entries)
    {
        long total = 0;
        foreach (var entry in entries)
        {
            total += entry.Query.Length * 2; // String chars
            total += entry.Response.Length * 2;
            total += entry.QueryEmbedding.Length * 4; // Float array
            total += 100; // Overhead estimate
        }
        return total;
    }

    private class CacheEntry
    {
        public string Key { get; set; } = "";
        public string Query { get; set; } = "";
        public float[] QueryEmbedding { get; set; } = Array.Empty<float>();
        public string Response { get; set; } = "";
        public Guid? WorkspaceId { get; set; }
        public Guid? UserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public List<Guid> SourceDocumentIds { get; set; } = new();
        public Dictionary<string, string> Metadata { get; set; } = new();
        public List<string> Tags { get; set; } = new();
        public double SimilarityScore { get; set; }
    }
}
