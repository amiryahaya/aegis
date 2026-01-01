using System.Collections.Concurrent;
using System.Security.Cryptography;
using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Security;

/// <summary>
/// In-memory API key service for development and testing
/// </summary>
public class InMemoryApiKeyService : IApiKeyService
{
    private readonly ILogger<InMemoryApiKeyService> _logger;
    private readonly ConcurrentDictionary<Guid, ApiKeyRecord> _keysById = new();
    private readonly ConcurrentDictionary<string, Guid> _keyHashToId = new();

    private const string KeyPrefix = "aegis_";
    private const int KeyLength = 32;

    public InMemoryApiKeyService(ILogger<InMemoryApiKeyService> logger)
    {
        _logger = logger;
    }

    public async Task<Result<ApiKeyResult>> GenerateAsync(
        ApiKeyRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Result<ApiKeyResult>.Failure(
                    Error.Validation("ApiKey.EmptyName", "API key name is required"));
            }

            var keyId = Guid.NewGuid();
            var rawKey = GenerateSecureKey();
            var fullKey = $"{KeyPrefix}{rawKey}";
            var keyHash = HashKey(fullKey);
            var lastFour = rawKey.Substring(rawKey.Length - 4);

            var record = new ApiKeyRecord
            {
                KeyId = keyId,
                Name = request.Name,
                KeyHash = keyHash,
                Prefix = KeyPrefix,
                LastFour = lastFour,
                UserId = request.UserId,
                TeamId = request.TeamId,
                Scopes = request.Scopes.ToList(),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = request.ExpiresAt,
                RateLimitTier = request.RateLimitTier,
                AllowedIps = request.AllowedIps?.ToList(),
                Metadata = new Dictionary<string, string>(request.Metadata),
                Description = request.Description,
                IsActive = true,
                IsRevoked = false
            };

            _keysById[keyId] = record;
            _keyHashToId[keyHash] = keyId;

            _logger.LogInformation("Generated API key {KeyId} for user {UserId}", keyId, request.UserId);

            return Result<ApiKeyResult>.Success(new ApiKeyResult
            {
                KeyId = keyId,
                ApiKey = fullKey,
                Prefix = KeyPrefix,
                LastFour = lastFour,
                Name = request.Name,
                CreatedAt = record.CreatedAt,
                ExpiresAt = request.ExpiresAt,
                Scopes = request.Scopes.ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating API key");
            return Result<ApiKeyResult>.Failure(
                Error.Internal("ApiKey.Error", ex.Message));
        }
    }

    public async Task<Result<ApiKeyValidation>> ValidateAsync(
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return Result<ApiKeyValidation>.Failure(
                    Error.Validation("ApiKey.Empty", "API key is required"));
            }

            // Check format
            if (!apiKey.StartsWith(KeyPrefix))
            {
                return Result<ApiKeyValidation>.Success(new ApiKeyValidation
                {
                    IsValid = false,
                    ReasonCode = ApiKeyInvalidReason.InvalidFormat,
                    InvalidReason = "Invalid API key format"
                });
            }

            var keyHash = HashKey(apiKey);

            // Look up the key
            if (!_keyHashToId.TryGetValue(keyHash, out var keyId))
            {
                return Result<ApiKeyValidation>.Success(new ApiKeyValidation
                {
                    IsValid = false,
                    ReasonCode = ApiKeyInvalidReason.NotFound,
                    InvalidReason = "API key not found"
                });
            }

            if (!_keysById.TryGetValue(keyId, out var record))
            {
                return Result<ApiKeyValidation>.Success(new ApiKeyValidation
                {
                    IsValid = false,
                    ReasonCode = ApiKeyInvalidReason.NotFound,
                    InvalidReason = "API key not found"
                });
            }

            // Check if revoked
            if (record.IsRevoked)
            {
                return Result<ApiKeyValidation>.Success(new ApiKeyValidation
                {
                    IsValid = false,
                    ReasonCode = ApiKeyInvalidReason.Revoked,
                    InvalidReason = "API key has been revoked"
                });
            }

            // Check if disabled
            if (!record.IsActive)
            {
                return Result<ApiKeyValidation>.Success(new ApiKeyValidation
                {
                    IsValid = false,
                    ReasonCode = ApiKeyInvalidReason.Disabled,
                    InvalidReason = "API key is disabled"
                });
            }

            // Check expiration
            if (record.ExpiresAt.HasValue && record.ExpiresAt.Value < DateTime.UtcNow)
            {
                return Result<ApiKeyValidation>.Success(new ApiKeyValidation
                {
                    IsValid = false,
                    ReasonCode = ApiKeyInvalidReason.Expired,
                    InvalidReason = "API key has expired"
                });
            }

            // Update usage stats
            record.LastUsedAt = DateTime.UtcNow;
            record.TotalRequests++;

            return Result<ApiKeyValidation>.Success(new ApiKeyValidation
            {
                IsValid = true,
                KeyInfo = ToKeyInfo(record)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating API key");
            return Result<ApiKeyValidation>.Failure(
                Error.Internal("ApiKey.Error", ex.Message));
        }
    }

    public async Task<Result> RevokeAsync(
        Guid keyId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_keysById.TryGetValue(keyId, out var record))
            {
                return Result.Failure(Error.NotFound("ApiKey.NotFound", "API key not found"));
            }

            record.IsRevoked = true;
            record.RevokedAt = DateTime.UtcNow;
            record.RevokeReason = reason;

            _logger.LogInformation("Revoked API key {KeyId}: {Reason}", keyId, reason);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking API key");
            return Result.Failure(Error.Internal("ApiKey.Error", ex.Message));
        }
    }

    public async Task<Result<List<ApiKeyInfo>>> ListAsync(
        Guid? userId,
        Guid? teamId,
        bool includeRevoked = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _keysById.Values.AsEnumerable();

            if (userId.HasValue)
            {
                query = query.Where(k => k.UserId == userId.Value);
            }

            if (teamId.HasValue)
            {
                query = query.Where(k => k.TeamId == teamId.Value);
            }

            if (!includeRevoked)
            {
                query = query.Where(k => !k.IsRevoked);
            }

            var keys = query.Select(ToKeyInfo).ToList();

            return Result<List<ApiKeyInfo>>.Success(keys);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing API keys");
            return Result<List<ApiKeyInfo>>.Failure(
                Error.Internal("ApiKey.Error", ex.Message));
        }
    }

    public async Task<Result<ApiKeyResult>> RotateAsync(
        Guid keyId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_keysById.TryGetValue(keyId, out var oldRecord))
            {
                return Result<ApiKeyResult>.Failure(
                    Error.NotFound("ApiKey.NotFound", "API key not found"));
            }

            // Revoke old key
            oldRecord.IsRevoked = true;
            oldRecord.RevokedAt = DateTime.UtcNow;
            oldRecord.RevokeReason = "Key rotated";

            // Generate new key with same properties
            var request = new ApiKeyRequest
            {
                Name = oldRecord.Name,
                UserId = oldRecord.UserId,
                TeamId = oldRecord.TeamId,
                Scopes = oldRecord.Scopes,
                ExpiresAt = oldRecord.ExpiresAt,
                RateLimitTier = oldRecord.RateLimitTier,
                AllowedIps = oldRecord.AllowedIps,
                Metadata = oldRecord.Metadata,
                Description = oldRecord.Description
            };

            var result = await GenerateAsync(request, cancellationToken);

            if (result.IsSuccess)
            {
                _logger.LogInformation("Rotated API key {OldKeyId} -> {NewKeyId}", keyId, result.Value.KeyId);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rotating API key");
            return Result<ApiKeyResult>.Failure(
                Error.Internal("ApiKey.Error", ex.Message));
        }
    }

    public async Task<Result<ApiKeyInfo>> UpdateAsync(
        Guid keyId,
        ApiKeyUpdate update,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_keysById.TryGetValue(keyId, out var record))
            {
                return Result<ApiKeyInfo>.Failure(
                    Error.NotFound("ApiKey.NotFound", "API key not found"));
            }

            if (update.Name != null)
                record.Name = update.Name;

            if (update.Scopes != null)
                record.Scopes = update.Scopes.ToList();

            if (update.ExpiresAt.HasValue)
                record.ExpiresAt = update.ExpiresAt;

            if (update.RateLimitTier.HasValue)
                record.RateLimitTier = update.RateLimitTier.Value;

            if (update.AllowedIps != null)
                record.AllowedIps = update.AllowedIps.ToList();

            if (update.Metadata != null)
                record.Metadata = new Dictionary<string, string>(update.Metadata);

            if (update.Description != null)
                record.Description = update.Description;

            if (update.IsActive.HasValue)
                record.IsActive = update.IsActive.Value;

            _logger.LogInformation("Updated API key {KeyId}", keyId);

            return Result<ApiKeyInfo>.Success(ToKeyInfo(record));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating API key");
            return Result<ApiKeyInfo>.Failure(
                Error.Internal("ApiKey.Error", ex.Message));
        }
    }

    private string GenerateSecureKey()
    {
        var bytes = new byte[KeyLength];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "")
            .Substring(0, KeyLength);
    }

    private string HashKey(string key)
    {
        using var sha256 = SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(key);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    private ApiKeyInfo ToKeyInfo(ApiKeyRecord record)
    {
        return new ApiKeyInfo
        {
            KeyId = record.KeyId,
            Name = record.Name,
            Prefix = record.Prefix,
            LastFour = record.LastFour,
            UserId = record.UserId,
            TeamId = record.TeamId,
            Scopes = record.Scopes,
            CreatedAt = record.CreatedAt,
            ExpiresAt = record.ExpiresAt,
            LastUsedAt = record.LastUsedAt,
            IsActive = record.IsActive,
            IsRevoked = record.IsRevoked,
            RateLimitTier = record.RateLimitTier,
            TotalRequests = record.TotalRequests,
            AllowedIps = record.AllowedIps,
            Metadata = record.Metadata,
            Description = record.Description
        };
    }

    private class ApiKeyRecord
    {
        public Guid KeyId { get; set; }
        public string Name { get; set; } = "";
        public string KeyHash { get; set; } = "";
        public string Prefix { get; set; } = "";
        public string LastFour { get; set; } = "";
        public Guid? UserId { get; set; }
        public Guid? TeamId { get; set; }
        public List<string> Scopes { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public bool IsActive { get; set; }
        public bool IsRevoked { get; set; }
        public DateTime? RevokedAt { get; set; }
        public string? RevokeReason { get; set; }
        public RateLimitTier RateLimitTier { get; set; }
        public long TotalRequests { get; set; }
        public List<string>? AllowedIps { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new();
        public string? Description { get; set; }
    }
}
