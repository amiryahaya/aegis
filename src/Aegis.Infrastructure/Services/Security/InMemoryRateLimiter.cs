using System.Collections.Concurrent;
using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Security;

/// <summary>
/// In-memory rate limiter using sliding window algorithm
/// </summary>
public class InMemoryRateLimiter : IRateLimiter
{
    private readonly ILogger<InMemoryRateLimiter> _logger;
    private readonly RateLimitConfig _config;
    private readonly ConcurrentDictionary<string, RateLimitBucket> _buckets = new();
    private readonly ConcurrentDictionary<string, long> _hitCounter = new();
    private readonly ConcurrentDictionary<string, long> _missCounter = new();

    public InMemoryRateLimiter(ILogger<InMemoryRateLimiter> logger, RateLimitConfig config)
    {
        _logger = logger;
        _config = config;
    }

    public async Task<Result<RateLimitResult>> CheckAsync(
        RateLimitRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Identifier))
            {
                return Result<RateLimitResult>.Failure(
                    Error.Validation("RateLimit.EmptyIdentifier", "Identifier cannot be empty"));
            }

            var key = GetBucketKey(request);
            var bucket = _buckets.GetOrAdd(key, _ => new RateLimitBucket());
            var now = DateTime.UtcNow;

            // Clean old entries
            bucket.CleanExpiredEntries(now, _config.Limits.Max(l => l.WindowSeconds));

            // Check against each window limit
            foreach (var limit in _config.Limits)
            {
                var windowStart = now.AddSeconds(-limit.WindowSeconds);
                var count = bucket.GetCountSince(windowStart);

                if (count >= limit.MaxRequests)
                {
                    var resetTime = bucket.GetOldestEntry()?.AddSeconds(limit.WindowSeconds) ?? now.AddSeconds(limit.WindowSeconds);
                    var retryAfter = (int)Math.Ceiling((resetTime - now).TotalSeconds);

                    return Result<RateLimitResult>.Success(new RateLimitResult
                    {
                        IsAllowed = false,
                        CurrentCount = count,
                        MaxRequests = limit.MaxRequests,
                        RemainingRequests = 0,
                        ResetInSeconds = retryAfter,
                        RetryAfterSeconds = Math.Max(1, retryAfter),
                        WindowSeconds = limit.WindowSeconds,
                        LimitType = request.LimitType,
                        Headers = BuildHeaders(count, limit.MaxRequests, 0, retryAfter)
                    });
                }
            }

            // All limits passed
            var primaryLimit = _config.Limits.First();
            var primaryWindowStart = now.AddSeconds(-primaryLimit.WindowSeconds);
            var currentCount = bucket.GetCountSince(primaryWindowStart);
            var remaining = primaryLimit.MaxRequests - currentCount;

            return Result<RateLimitResult>.Success(new RateLimitResult
            {
                IsAllowed = true,
                CurrentCount = currentCount,
                MaxRequests = primaryLimit.MaxRequests,
                RemainingRequests = remaining,
                ResetInSeconds = primaryLimit.WindowSeconds,
                RetryAfterSeconds = 0,
                WindowSeconds = primaryLimit.WindowSeconds,
                LimitType = request.LimitType,
                Headers = BuildHeaders(currentCount, primaryLimit.MaxRequests, remaining, primaryLimit.WindowSeconds)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking rate limit");
            return Result<RateLimitResult>.Failure(
                Error.Internal("RateLimit.Error", ex.Message));
        }
    }

    public async Task<Result<RateLimitResult>> RecordRequestAsync(
        RateLimitRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Identifier))
            {
                return Result<RateLimitResult>.Failure(
                    Error.Validation("RateLimit.EmptyIdentifier", "Identifier cannot be empty"));
            }

            var cost = Math.Max(0, request.Cost);
            if (cost == 0)
            {
                // Zero cost requests don't count
                return await CheckAsync(request, cancellationToken);
            }

            var key = GetBucketKey(request);
            var bucket = _buckets.GetOrAdd(key, _ => new RateLimitBucket());
            var now = DateTime.UtcNow;

            // Record the request(s) based on cost
            for (int i = 0; i < cost; i++)
            {
                bucket.AddEntry(now);
            }

            // Clean old entries
            bucket.CleanExpiredEntries(now, _config.Limits.Max(l => l.WindowSeconds));

            // Return current status
            return await CheckAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording request");
            return Result<RateLimitResult>.Failure(
                Error.Internal("RateLimit.Error", ex.Message));
        }
    }

    public async Task<Result<RateLimitStatus>> GetStatusAsync(
        string identifier,
        RateLimitType limitType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var key = $"{limitType}:{identifier}";
            var now = DateTime.UtcNow;
            var windows = new Dictionary<string, WindowStatus>();
            var isBlocked = false;
            DateTime? blockExpiresAt = null;

            if (_buckets.TryGetValue(key, out var bucket))
            {
                foreach (var limit in _config.Limits)
                {
                    var windowStart = now.AddSeconds(-limit.WindowSeconds);
                    var count = bucket.GetCountSince(windowStart);
                    var resetsAt = bucket.GetOldestEntry()?.AddSeconds(limit.WindowSeconds) ?? now.AddSeconds(limit.WindowSeconds);

                    if (count >= limit.MaxRequests)
                    {
                        isBlocked = true;
                        if (blockExpiresAt == null || resetsAt > blockExpiresAt)
                        {
                            blockExpiresAt = resetsAt;
                        }
                    }

                    windows[limit.DisplayName] = new WindowStatus
                    {
                        WindowName = limit.DisplayName,
                        Count = count,
                        Limit = limit.MaxRequests,
                        ResetsAt = resetsAt
                    };
                }
            }

            return Result<RateLimitStatus>.Success(new RateLimitStatus
            {
                Identifier = identifier,
                LimitType = limitType,
                Windows = windows,
                IsBlocked = isBlocked,
                BlockReason = isBlocked ? "Rate limit exceeded" : null,
                BlockExpiresAt = blockExpiresAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting status");
            return Result<RateLimitStatus>.Failure(
                Error.Internal("RateLimit.Error", ex.Message));
        }
    }

    public async Task<Result> ResetAsync(
        string identifier,
        RateLimitType limitType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var key = $"{limitType}:{identifier}";
            _buckets.TryRemove(key, out _);

            _logger.LogInformation("Reset rate limits for {Key}", key);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting rate limit");
            return Result.Failure(Error.Internal("RateLimit.Error", ex.Message));
        }
    }

    private string GetBucketKey(RateLimitRequest request)
    {
        var identifier = request.LimitType switch
        {
            RateLimitType.Resource => $"{request.Identifier}:{request.Resource}",
            _ => request.Identifier
        };

        return $"{request.LimitType}:{identifier}";
    }

    private Dictionary<string, string> BuildHeaders(int count, int limit, int remaining, int reset)
    {
        return new Dictionary<string, string>
        {
            ["X-RateLimit-Limit"] = limit.ToString(),
            ["X-RateLimit-Remaining"] = Math.Max(0, remaining).ToString(),
            ["X-RateLimit-Reset"] = DateTimeOffset.UtcNow.AddSeconds(reset).ToUnixTimeSeconds().ToString()
        };
    }

    private class RateLimitBucket
    {
        private readonly List<DateTime> _entries = new();
        private readonly object _lock = new();

        public void AddEntry(DateTime timestamp)
        {
            lock (_lock)
            {
                _entries.Add(timestamp);
            }
        }

        public int GetCountSince(DateTime since)
        {
            lock (_lock)
            {
                return _entries.Count(e => e >= since);
            }
        }

        public DateTime? GetOldestEntry()
        {
            lock (_lock)
            {
                return _entries.Count > 0 ? _entries.Min() : null;
            }
        }

        public void CleanExpiredEntries(DateTime now, int maxWindowSeconds)
        {
            var cutoff = now.AddSeconds(-maxWindowSeconds);
            lock (_lock)
            {
                _entries.RemoveAll(e => e < cutoff);
            }
        }
    }
}
