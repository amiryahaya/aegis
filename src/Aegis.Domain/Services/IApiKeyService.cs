using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Service for managing API keys
/// </summary>
public interface IApiKeyService
{
    /// <summary>
    /// Generate a new API key
    /// </summary>
    /// <param name="request">API key creation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Generated API key details</returns>
    Task<Result<ApiKeyResult>> GenerateAsync(
        ApiKeyRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate an API key
    /// </summary>
    /// <param name="apiKey">The API key to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result with key details</returns>
    Task<Result<ApiKeyValidation>> ValidateAsync(
        string apiKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revoke an API key
    /// </summary>
    /// <param name="keyId">The key ID to revoke</param>
    /// <param name="reason">Reason for revocation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success result</returns>
    Task<Result> RevokeAsync(
        Guid keyId,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// List API keys for a user or team
    /// </summary>
    /// <param name="userId">User ID (optional)</param>
    /// <param name="teamId">Team ID (optional)</param>
    /// <param name="includeRevoked">Whether to include revoked keys</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of API keys</returns>
    Task<Result<List<ApiKeyInfo>>> ListAsync(
        Guid? userId,
        Guid? teamId,
        bool includeRevoked = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rotate an API key (revoke old and generate new)
    /// </summary>
    /// <param name="keyId">The key ID to rotate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>New API key details</returns>
    Task<Result<ApiKeyResult>> RotateAsync(
        Guid keyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update API key permissions or metadata
    /// </summary>
    /// <param name="keyId">The key ID to update</param>
    /// <param name="update">Update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated key info</returns>
    Task<Result<ApiKeyInfo>> UpdateAsync(
        Guid keyId,
        ApiKeyUpdate update,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Request to create an API key
/// </summary>
public record ApiKeyRequest
{
    /// <summary>
    /// Display name for the key
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// User ID who owns the key
    /// </summary>
    public Guid? UserId { get; init; }

    /// <summary>
    /// Team ID who owns the key
    /// </summary>
    public Guid? TeamId { get; init; }

    /// <summary>
    /// Scopes/permissions for the key
    /// </summary>
    public List<string> Scopes { get; init; } = new() { "read", "write" };

    /// <summary>
    /// Key expiration (null = never expires)
    /// </summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>
    /// Rate limit tier for the key
    /// </summary>
    public RateLimitTier RateLimitTier { get; init; } = RateLimitTier.Standard;

    /// <summary>
    /// Allowed IP addresses (null = any)
    /// </summary>
    public List<string>? AllowedIps { get; init; }

    /// <summary>
    /// Custom metadata
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = new();

    /// <summary>
    /// Description of the key's purpose
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
/// Result of API key generation
/// </summary>
public record ApiKeyResult
{
    /// <summary>
    /// Unique key ID
    /// </summary>
    public Guid KeyId { get; init; }

    /// <summary>
    /// The actual API key (only shown once)
    /// </summary>
    public required string ApiKey { get; init; }

    /// <summary>
    /// Key prefix for identification (e.g., "aegis_")
    /// </summary>
    public required string Prefix { get; init; }

    /// <summary>
    /// Last 4 characters for display
    /// </summary>
    public required string LastFour { get; init; }

    /// <summary>
    /// Display name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// When the key was created
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// When the key expires
    /// </summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>
    /// Granted scopes
    /// </summary>
    public List<string> Scopes { get; init; } = new();
}

/// <summary>
/// Result of API key validation
/// </summary>
public record ApiKeyValidation
{
    /// <summary>
    /// Whether the key is valid
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Key information (if valid)
    /// </summary>
    public ApiKeyInfo? KeyInfo { get; init; }

    /// <summary>
    /// Reason for invalidity
    /// </summary>
    public string? InvalidReason { get; init; }

    /// <summary>
    /// Invalid reason code
    /// </summary>
    public ApiKeyInvalidReason? ReasonCode { get; init; }
}

/// <summary>
/// Reasons for API key invalidity
/// </summary>
public enum ApiKeyInvalidReason
{
    NotFound,
    Expired,
    Revoked,
    IpNotAllowed,
    RateLimitExceeded,
    InvalidFormat,
    Disabled
}

/// <summary>
/// API key information
/// </summary>
public record ApiKeyInfo
{
    /// <summary>
    /// Unique key ID
    /// </summary>
    public Guid KeyId { get; init; }

    /// <summary>
    /// Display name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Key prefix
    /// </summary>
    public required string Prefix { get; init; }

    /// <summary>
    /// Last 4 characters
    /// </summary>
    public required string LastFour { get; init; }

    /// <summary>
    /// Owner user ID
    /// </summary>
    public Guid? UserId { get; init; }

    /// <summary>
    /// Owner team ID
    /// </summary>
    public Guid? TeamId { get; init; }

    /// <summary>
    /// Granted scopes
    /// </summary>
    public List<string> Scopes { get; init; } = new();

    /// <summary>
    /// When the key was created
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// When the key expires
    /// </summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>
    /// When the key was last used
    /// </summary>
    public DateTime? LastUsedAt { get; init; }

    /// <summary>
    /// Whether the key is currently active
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// Whether the key is revoked
    /// </summary>
    public bool IsRevoked { get; init; }

    /// <summary>
    /// Rate limit tier
    /// </summary>
    public RateLimitTier RateLimitTier { get; init; }

    /// <summary>
    /// Total number of requests made
    /// </summary>
    public long TotalRequests { get; init; }

    /// <summary>
    /// Allowed IP addresses
    /// </summary>
    public List<string>? AllowedIps { get; init; }

    /// <summary>
    /// Custom metadata
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = new();

    /// <summary>
    /// Description
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
/// Rate limit tiers for API keys
/// </summary>
public enum RateLimitTier
{
    Free,       // 100 requests/minute
    Standard,   // 1000 requests/minute
    Premium,    // 5000 requests/minute
    Enterprise, // 10000 requests/minute
    Unlimited   // No rate limit
}

/// <summary>
/// Update request for API key
/// </summary>
public record ApiKeyUpdate
{
    /// <summary>
    /// New display name
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// New scopes
    /// </summary>
    public List<string>? Scopes { get; init; }

    /// <summary>
    /// New expiration date
    /// </summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>
    /// New rate limit tier
    /// </summary>
    public RateLimitTier? RateLimitTier { get; init; }

    /// <summary>
    /// New allowed IPs
    /// </summary>
    public List<string>? AllowedIps { get; init; }

    /// <summary>
    /// Updated metadata
    /// </summary>
    public Dictionary<string, string>? Metadata { get; init; }

    /// <summary>
    /// New description
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Enable or disable the key
    /// </summary>
    public bool? IsActive { get; init; }
}
