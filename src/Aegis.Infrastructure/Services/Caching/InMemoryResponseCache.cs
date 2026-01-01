using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Caching;

/// <summary>
/// In-memory LLM response cache implementation
/// </summary>
public class InMemoryResponseCache : IResponseCache
{
    private readonly ILogger<InMemoryResponseCache> _logger;
    private readonly ConcurrentDictionary<string, ResponseEntry> _cache = new();
    private long _hits;
    private long _misses;
    private long _totalLatencySaved;
    private decimal _totalCostSaved;

    public InMemoryResponseCache(ILogger<InMemoryResponseCache> logger)
    {
        _logger = logger;
    }

    public async Task<Result<ResponseCacheResult>> GetAsync(
        ResponseCacheRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Prompt))
            {
                return Result<ResponseCacheResult>.Failure(
                    Error.Validation("ResponseCache.EmptyPrompt", "Prompt cannot be empty"));
            }

            if (string.IsNullOrEmpty(request.Model))
            {
                return Result<ResponseCacheResult>.Failure(
                    Error.Validation("ResponseCache.EmptyModel", "Model cannot be empty"));
            }

            var key = GenerateKey(request);

            if (_cache.TryGetValue(key, out var entry))
            {
                // Check expiration
                if (entry.ExpiresAt.HasValue && entry.ExpiresAt.Value < DateTime.UtcNow)
                {
                    _cache.TryRemove(key, out _);
                    Interlocked.Increment(ref _misses);
                    return Result<ResponseCacheResult>.Success(new ResponseCacheResult { IsHit = false });
                }

                Interlocked.Increment(ref _hits);
                Interlocked.Add(ref _totalLatencySaved, entry.LatencyMs);
                _totalCostSaved += entry.EstimatedCost;

                var remainingTtl = entry.ExpiresAt.HasValue
                    ? (int)(entry.ExpiresAt.Value - DateTime.UtcNow).TotalSeconds
                    : (int?)null;

                return Result<ResponseCacheResult>.Success(new ResponseCacheResult
                {
                    IsHit = true,
                    Response = entry.Response,
                    Model = entry.Model,
                    TokenCount = entry.OutputTokens,
                    CachedAt = entry.CachedAt,
                    TtlSeconds = remainingTtl,
                    CacheKey = key,
                    IsFuzzyMatch = false,
                    MatchScore = 1.0,
                    OriginalLatencyMs = entry.LatencyMs,
                    CostSavings = entry.EstimatedCost
                });
            }

            Interlocked.Increment(ref _misses);
            return Result<ResponseCacheResult>.Success(new ResponseCacheResult { IsHit = false });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting from response cache");
            return Result<ResponseCacheResult>.Failure(
                Error.Internal("ResponseCache.Error", ex.Message));
        }
    }

    public async Task<Result> SetAsync(
        ResponseCacheEntry entry,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var key = GenerateKey(new ResponseCacheRequest
            {
                Prompt = entry.Prompt,
                SystemPrompt = entry.SystemPrompt,
                Model = entry.Model,
                Temperature = entry.Temperature,
                Context = entry.Context,
                WorkspaceId = entry.WorkspaceId
            });

            var cacheEntry = new ResponseEntry
            {
                Key = key,
                Prompt = entry.Prompt,
                SystemPrompt = entry.SystemPrompt,
                Response = entry.Response,
                Model = entry.Model,
                Temperature = entry.Temperature,
                Context = entry.Context,
                WorkspaceId = entry.WorkspaceId,
                CachedAt = DateTime.UtcNow,
                ExpiresAt = entry.TtlSeconds > 0 ? DateTime.UtcNow.AddSeconds(entry.TtlSeconds) : null,
                InputTokens = entry.InputTokens,
                OutputTokens = entry.OutputTokens,
                LatencyMs = entry.LatencyMs,
                EstimatedCost = entry.EstimatedCost,
                Tags = entry.Tags.ToList()
            };

            _cache[key] = cacheEntry;

            _logger.LogDebug("Cached LLM response for model {Model}", entry.Model);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting response cache");
            return Result.Failure(Error.Internal("ResponseCache.Error", ex.Message));
        }
    }

    public async Task<Result<int>> InvalidateAsync(
        ResponseInvalidationPattern pattern,
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

            _logger.LogInformation("Invalidated {Count} response cache entries", keysToRemove.Count);

            return Result<int>.Success(keysToRemove.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating response cache");
            return Result<int>.Failure(Error.Internal("ResponseCache.Error", ex.Message));
        }
    }

    public async Task<Result<ResponseCacheStatistics>> GetStatisticsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entries = _cache.Values.ToList();

            var entriesByModel = entries
                .GroupBy(e => e.Model)
                .ToDictionary(g => g.Key, g => (long)g.Count());

            var totalTokens = entries.Sum(e => (long)e.OutputTokens);
            var estimatedMemory = entries.Sum(e =>
                (long)(e.Prompt.Length + e.Response.Length + (e.SystemPrompt?.Length ?? 0)) * 2 + 100);

            return Result<ResponseCacheStatistics>.Success(new ResponseCacheStatistics
            {
                TotalEntries = entries.Count,
                TotalHits = _hits,
                TotalMisses = _misses,
                EntriesByModel = entriesByModel,
                TotalTokensCached = totalTokens,
                ApiCallsSaved = _hits,
                TotalLatencySavedMs = _totalLatencySaved,
                TotalCostSavings = _totalCostSaved,
                MemoryUsageBytes = estimatedMemory
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting response cache statistics");
            return Result<ResponseCacheStatistics>.Failure(
                Error.Internal("ResponseCache.Error", ex.Message));
        }
    }

    public Task<Result<ResponseCacheStatistics>> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        return GetStatisticsAsync(cancellationToken);
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

            _logger.LogDebug("Evicted {Count} expired response cache entries", keysToRemove.Count);

            return Task.FromResult(Result<int>.Success(keysToRemove.Count));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evicting expired cache entries");
            return Task.FromResult(Result<int>.Failure(Error.Internal("ResponseCache.Error", ex.Message)));
        }
    }

    private string GenerateKey(ResponseCacheRequest request)
    {
        var keyParts = new StringBuilder();
        keyParts.Append(request.Model);
        keyParts.Append(':');
        keyParts.Append(request.Temperature.ToString("F2"));
        keyParts.Append(':');
        keyParts.Append(ComputeHash(request.Prompt));

        if (!string.IsNullOrEmpty(request.SystemPrompt))
        {
            keyParts.Append(':');
            keyParts.Append(ComputeHash(request.SystemPrompt));
        }

        if (!string.IsNullOrEmpty(request.Context))
        {
            keyParts.Append(':');
            keyParts.Append(ComputeHash(request.Context));
        }

        if (request.WorkspaceId.HasValue)
        {
            keyParts.Append(':');
            keyParts.Append(request.WorkspaceId.Value);
        }

        return keyParts.ToString();
    }

    private string ComputeHash(string text)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash).Substring(0, 16);
    }

    private bool ShouldInvalidate(ResponseEntry entry, ResponseInvalidationPattern pattern)
    {
        if (pattern.InvalidateAll) return true;

        if (!string.IsNullOrEmpty(pattern.Model) && entry.Model == pattern.Model)
            return true;

        if (pattern.WorkspaceId.HasValue && entry.WorkspaceId == pattern.WorkspaceId)
            return true;

        if (pattern.CacheKeys?.Contains(entry.Key) == true)
            return true;

        if (pattern.Tags?.Any(tag => entry.Tags.Contains(tag)) == true)
            return true;

        if (pattern.OlderThan.HasValue && entry.CachedAt < pattern.OlderThan)
            return true;

        return false;
    }

    private class ResponseEntry
    {
        public string Key { get; set; } = "";
        public string Prompt { get; set; } = "";
        public string? SystemPrompt { get; set; }
        public string Response { get; set; } = "";
        public string Model { get; set; } = "";
        public double Temperature { get; set; }
        public string? Context { get; set; }
        public Guid? WorkspaceId { get; set; }
        public DateTime CachedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public int LatencyMs { get; set; }
        public decimal EstimatedCost { get; set; }
        public List<string> Tags { get; set; } = new();
    }
}
