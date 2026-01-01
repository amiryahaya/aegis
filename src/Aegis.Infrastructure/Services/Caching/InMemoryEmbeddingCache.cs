using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Caching;

/// <summary>
/// In-memory embedding cache implementation
/// </summary>
public class InMemoryEmbeddingCache : IEmbeddingCache
{
    private readonly ILogger<InMemoryEmbeddingCache> _logger;
    private readonly ConcurrentDictionary<string, EmbeddingEntry> _cache = new();
    private long _hits;
    private long _misses;

    public InMemoryEmbeddingCache(ILogger<InMemoryEmbeddingCache> logger)
    {
        _logger = logger;
    }

    public async Task<Result<EmbeddingCacheResult>> GetAsync(
        string text,
        string model,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(text))
            {
                return Result<EmbeddingCacheResult>.Failure(
                    Error.Validation("EmbeddingCache.EmptyText", "Text cannot be empty"));
            }

            var key = GenerateKey(text, model);

            if (_cache.TryGetValue(key, out var entry))
            {
                // Check expiration
                if (entry.ExpiresAt.HasValue && entry.ExpiresAt.Value < DateTime.UtcNow)
                {
                    _cache.TryRemove(key, out _);
                    Interlocked.Increment(ref _misses);
                    return Result<EmbeddingCacheResult>.Success(new EmbeddingCacheResult { IsHit = false });
                }

                Interlocked.Increment(ref _hits);
                return Result<EmbeddingCacheResult>.Success(new EmbeddingCacheResult
                {
                    IsHit = true,
                    Embedding = entry.Embedding,
                    Model = entry.Model,
                    CachedAt = entry.CachedAt,
                    TextHash = entry.TextHash,
                    Dimension = entry.Embedding.Length
                });
            }

            Interlocked.Increment(ref _misses);
            return Result<EmbeddingCacheResult>.Success(new EmbeddingCacheResult { IsHit = false });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting from embedding cache");
            return Result<EmbeddingCacheResult>.Failure(
                Error.Internal("EmbeddingCache.Error", ex.Message));
        }
    }

    public async Task<Result<BatchEmbeddingCacheResult>> GetBatchAsync(
        List<string> texts,
        string model,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (texts.Count == 0)
            {
                return Result<BatchEmbeddingCacheResult>.Success(new BatchEmbeddingCacheResult());
            }

            var results = new List<EmbeddingCacheResult>();
            var missIndices = new List<int>();
            var missedTexts = new List<string>();
            var hitCount = 0;
            var missCount = 0;

            for (int i = 0; i < texts.Count; i++)
            {
                var result = await GetAsync(texts[i], model, cancellationToken);

                if (result.IsSuccess)
                {
                    results.Add(result.Value);
                    if (result.Value.IsHit)
                    {
                        hitCount++;
                    }
                    else
                    {
                        missCount++;
                        missIndices.Add(i);
                        missedTexts.Add(texts[i]);
                    }
                }
                else
                {
                    results.Add(new EmbeddingCacheResult { IsHit = false });
                    missCount++;
                    missIndices.Add(i);
                    missedTexts.Add(texts[i]);
                }
            }

            return Result<BatchEmbeddingCacheResult>.Success(new BatchEmbeddingCacheResult
            {
                Results = results,
                HitCount = hitCount,
                MissCount = missCount,
                MissIndices = missIndices,
                MissedTexts = missedTexts
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting batch from embedding cache");
            return Result<BatchEmbeddingCacheResult>.Failure(
                Error.Internal("EmbeddingCache.Error", ex.Message));
        }
    }

    public async Task<Result> SetAsync(
        EmbeddingCacheEntry entry,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (entry.Embedding == null || entry.Embedding.Length == 0)
            {
                return Result.Failure(
                    Error.Validation("EmbeddingCache.EmptyEmbedding", "Embedding cannot be empty"));
            }

            var key = GenerateKey(entry.Text, entry.Model);
            var textHash = ComputeHash(entry.Text);

            var cacheEntry = new EmbeddingEntry
            {
                Key = key,
                TextHash = textHash,
                Embedding = entry.Embedding,
                Model = entry.Model,
                CachedAt = DateTime.UtcNow,
                ExpiresAt = entry.TtlSeconds > 0 ? DateTime.UtcNow.AddSeconds(entry.TtlSeconds) : null,
                DocumentId = entry.DocumentId,
                ChunkIndex = entry.ChunkIndex
            };

            _cache[key] = cacheEntry;

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting embedding cache");
            return Result.Failure(Error.Internal("EmbeddingCache.Error", ex.Message));
        }
    }

    public async Task<Result<int>> SetBatchAsync(
        List<EmbeddingCacheEntry> entries,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var count = 0;
            foreach (var entry in entries)
            {
                var result = await SetAsync(entry, cancellationToken);
                if (result.IsSuccess)
                {
                    count++;
                }
            }

            return Result<int>.Success(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting batch embedding cache");
            return Result<int>.Failure(Error.Internal("EmbeddingCache.Error", ex.Message));
        }
    }

    public async Task<Result<int>> InvalidateAsync(
        EmbeddingInvalidationPattern pattern,
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

            _logger.LogInformation("Invalidated {Count} embedding cache entries", keysToRemove.Count);

            return Result<int>.Success(keysToRemove.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating embedding cache");
            return Result<int>.Failure(Error.Internal("EmbeddingCache.Error", ex.Message));
        }
    }

    public async Task<Result<EmbeddingCacheStatistics>> GetStatisticsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entries = _cache.Values.ToList();

            var entriesByModel = entries
                .GroupBy(e => e.Model)
                .ToDictionary(g => g.Key, g => (long)g.Count());

            var estimatedMemory = entries.Sum(e => e.Embedding.Length * 4L + 100);

            return Result<EmbeddingCacheStatistics>.Success(new EmbeddingCacheStatistics
            {
                TotalEntries = entries.Count,
                TotalHits = _hits,
                TotalMisses = _misses,
                EntriesByModel = entriesByModel,
                MemoryUsageBytes = estimatedMemory,
                ApiCallsSaved = _hits,
                EstimatedCostSavings = _hits * 0.0001m // Rough estimate
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting embedding cache statistics");
            return Result<EmbeddingCacheStatistics>.Failure(
                Error.Internal("EmbeddingCache.Error", ex.Message));
        }
    }

    private string GenerateKey(string text, string model)
    {
        var hash = ComputeHash(text);
        return $"{model}:{hash}";
    }

    private string ComputeHash(string text)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash).Substring(0, 16);
    }

    private bool ShouldInvalidate(EmbeddingEntry entry, EmbeddingInvalidationPattern pattern)
    {
        if (pattern.InvalidateAll) return true;

        if (!string.IsNullOrEmpty(pattern.Model) && entry.Model == pattern.Model)
            return true;

        if (pattern.DocumentIds?.Contains(entry.DocumentId ?? Guid.Empty) == true)
            return true;

        if (pattern.TextHashes?.Contains(entry.TextHash) == true)
            return true;

        if (pattern.OlderThan.HasValue && entry.CachedAt < pattern.OlderThan)
            return true;

        return false;
    }

    private class EmbeddingEntry
    {
        public string Key { get; set; } = "";
        public string TextHash { get; set; } = "";
        public float[] Embedding { get; set; } = Array.Empty<float>();
        public string Model { get; set; } = "";
        public DateTime CachedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public Guid? DocumentId { get; set; }
        public int? ChunkIndex { get; set; }
    }
}
