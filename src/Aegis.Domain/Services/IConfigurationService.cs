using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Service for managing dynamic system configuration
/// </summary>
public interface IConfigurationService
{
    /// <summary>
    /// Gets a configuration value
    /// </summary>
    Task<Result<T>> GetAsync<T>(
        string key,
        T defaultValue,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a configuration value for a specific category
    /// </summary>
    Task<Result<T>> GetAsync<T>(
        ConfigurationCategory category,
        string key,
        T defaultValue,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a configuration value
    /// </summary>
    Task<Result> SetAsync<T>(
        string key,
        T value,
        Guid? changedBy = null,
        string? reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a configuration value for a specific category
    /// </summary>
    Task<Result> SetAsync<T>(
        ConfigurationCategory category,
        string key,
        T value,
        Guid? changedBy = null,
        string? reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all configuration entries for a category
    /// </summary>
    Task<Result<IReadOnlyList<ConfigurationEntry>>> GetByCategoryAsync(
        ConfigurationCategory category,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all configuration entries
    /// </summary>
    Task<Result<IReadOnlyList<ConfigurationEntry>>> GetAllAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a configuration entry
    /// </summary>
    Task<Result> DeleteAsync(
        string key,
        Guid? changedBy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets a category to default values
    /// </summary>
    Task<Result> ResetCategoryAsync(
        ConfigurationCategory category,
        Guid? changedBy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets audit history for configuration changes
    /// </summary>
    Task<Result<IReadOnlyList<ConfigurationAuditEntry>>> GetAuditHistoryAsync(
        string? key = null,
        ConfigurationCategory? category = null,
        int limit = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a configuration value against schema
    /// </summary>
    Task<Result> ValidateAsync<T>(
        string key,
        T value,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports configuration from a dictionary
    /// </summary>
    Task<Result<int>> ImportAsync(
        Dictionary<string, object> configuration,
        bool overwrite = false,
        Guid? changedBy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports all configuration as a dictionary
    /// </summary>
    Task<Result<Dictionary<string, object>>> ExportAsync(
        ConfigurationCategory? category = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Categories for system configuration
/// </summary>
public enum ConfigurationCategory
{
    System,
    Llm,
    Cache,
    RateLimit,
    Webhook,
    Security,
    Storage,
    Search,
    Observability
}

/// <summary>
/// A configuration entry
/// </summary>
public record ConfigurationEntry
{
    public required string Key { get; init; }
    public ConfigurationCategory Category { get; init; } = ConfigurationCategory.System;
    public required object Value { get; init; }
    public ConfigurationValueType ValueType { get; init; } = ConfigurationValueType.String;
    public string? Description { get; init; }
    public bool IsSecret { get; init; }
    public bool IsReadOnly { get; init; }
    public object? DefaultValue { get; init; }
    public object? MinValue { get; init; }
    public object? MaxValue { get; init; }
    public IReadOnlyList<object>? AllowedValues { get; init; }
    public string? ValidationPattern { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; init; }
    public Guid? UpdatedBy { get; init; }
}

/// <summary>
/// Types of configuration values
/// </summary>
public enum ConfigurationValueType
{
    String,
    Integer,
    Double,
    Boolean,
    Json,
    StringList,
    Duration
}

/// <summary>
/// Audit entry for configuration changes
/// </summary>
public record ConfigurationAuditEntry
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string Key { get; init; }
    public ConfigurationCategory Category { get; init; }
    public required ConfigurationAuditAction Action { get; init; }
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
    public Guid? ChangedBy { get; init; }
    public DateTime ChangedAt { get; init; } = DateTime.UtcNow;
    public string? Reason { get; init; }
}

/// <summary>
/// Types of configuration audit actions
/// </summary>
public enum ConfigurationAuditAction
{
    Created,
    Updated,
    Deleted,
    Reset,
    Imported
}

/// <summary>
/// Well-known configuration keys
/// </summary>
public static class ConfigurationKeys
{
    // System
    public const string SystemMaintenanceMode = "system.maintenanceMode";
    public const string SystemMaxConcurrentRequests = "system.maxConcurrentRequests";
    public const string SystemRequestTimeout = "system.requestTimeout";
    public const string SystemDebugMode = "system.debugMode";

    // LLM
    public const string LlmDefaultModel = "llm.defaultModel";
    public const string LlmDefaultTemperature = "llm.defaultTemperature";
    public const string LlmMaxTokens = "llm.maxTokens";
    public const string LlmTimeout = "llm.timeout";
    public const string LlmRetryAttempts = "llm.retryAttempts";
    public const string LlmRateLimitPerMinute = "llm.rateLimitPerMinute";

    // Cache
    public const string CacheDefaultTtl = "cache.defaultTtl";
    public const string CacheMaxSize = "cache.maxSize";
    public const string CacheEnabled = "cache.enabled";
    public const string CacheSemanticThreshold = "cache.semanticThreshold";

    // Rate Limit
    public const string RateLimitEnabled = "rateLimit.enabled";
    public const string RateLimitRequestsPerMinute = "rateLimit.requestsPerMinute";
    public const string RateLimitBurstSize = "rateLimit.burstSize";
    public const string RateLimitWindowSeconds = "rateLimit.windowSeconds";

    // Webhook
    public const string WebhookEnabled = "webhook.enabled";
    public const string WebhookMaxRetries = "webhook.maxRetries";
    public const string WebhookTimeout = "webhook.timeout";
    public const string WebhookBatchSize = "webhook.batchSize";

    // Security
    public const string SecurityApiKeyExpirationDays = "security.apiKeyExpirationDays";
    public const string SecurityMaxLoginAttempts = "security.maxLoginAttempts";
    public const string SecurityLockoutDuration = "security.lockoutDuration";
    public const string SecurityRequireMfa = "security.requireMfa";

    // Storage
    public const string StorageMaxFileSizeMb = "storage.maxFileSizeMb";
    public const string StorageAllowedTypes = "storage.allowedTypes";
    public const string StorageRetentionDays = "storage.retentionDays";

    // Search
    public const string SearchDefaultResultCount = "search.defaultResultCount";
    public const string SearchMaxResultCount = "search.maxResultCount";
    public const string SearchMinScore = "search.minScore";
    public const string SearchEnableReranking = "search.enableReranking";

    // Observability
    public const string ObservabilityMetricsEnabled = "observability.metricsEnabled";
    public const string ObservabilityTracingEnabled = "observability.tracingEnabled";
    public const string ObservabilityLogLevel = "observability.logLevel";
    public const string ObservabilitySampleRate = "observability.sampleRate";
}

/// <summary>
/// Default configuration values
/// </summary>
public static class ConfigurationDefaults
{
    public static readonly Dictionary<string, object> Values = new()
    {
        // System
        [ConfigurationKeys.SystemMaintenanceMode] = false,
        [ConfigurationKeys.SystemMaxConcurrentRequests] = 100,
        [ConfigurationKeys.SystemRequestTimeout] = 30000, // ms
        [ConfigurationKeys.SystemDebugMode] = false,

        // LLM
        [ConfigurationKeys.LlmDefaultModel] = "gpt-4",
        [ConfigurationKeys.LlmDefaultTemperature] = 0.7,
        [ConfigurationKeys.LlmMaxTokens] = 4096,
        [ConfigurationKeys.LlmTimeout] = 120000, // ms
        [ConfigurationKeys.LlmRetryAttempts] = 3,
        [ConfigurationKeys.LlmRateLimitPerMinute] = 60,

        // Cache
        [ConfigurationKeys.CacheDefaultTtl] = 3600, // seconds
        [ConfigurationKeys.CacheMaxSize] = 10000,
        [ConfigurationKeys.CacheEnabled] = true,
        [ConfigurationKeys.CacheSemanticThreshold] = 0.95,

        // Rate Limit
        [ConfigurationKeys.RateLimitEnabled] = true,
        [ConfigurationKeys.RateLimitRequestsPerMinute] = 60,
        [ConfigurationKeys.RateLimitBurstSize] = 10,
        [ConfigurationKeys.RateLimitWindowSeconds] = 60,

        // Webhook
        [ConfigurationKeys.WebhookEnabled] = true,
        [ConfigurationKeys.WebhookMaxRetries] = 5,
        [ConfigurationKeys.WebhookTimeout] = 30000, // ms
        [ConfigurationKeys.WebhookBatchSize] = 100,

        // Security
        [ConfigurationKeys.SecurityApiKeyExpirationDays] = 365,
        [ConfigurationKeys.SecurityMaxLoginAttempts] = 5,
        [ConfigurationKeys.SecurityLockoutDuration] = 900, // seconds
        [ConfigurationKeys.SecurityRequireMfa] = false,

        // Storage
        [ConfigurationKeys.StorageMaxFileSizeMb] = 50,
        [ConfigurationKeys.StorageAllowedTypes] = "pdf,docx,txt,md,html,xlsx,csv",
        [ConfigurationKeys.StorageRetentionDays] = 365,

        // Search
        [ConfigurationKeys.SearchDefaultResultCount] = 10,
        [ConfigurationKeys.SearchMaxResultCount] = 100,
        [ConfigurationKeys.SearchMinScore] = 0.5,
        [ConfigurationKeys.SearchEnableReranking] = true,

        // Observability
        [ConfigurationKeys.ObservabilityMetricsEnabled] = true,
        [ConfigurationKeys.ObservabilityTracingEnabled] = true,
        [ConfigurationKeys.ObservabilityLogLevel] = "Information",
        [ConfigurationKeys.ObservabilitySampleRate] = 1.0
    };

    public static ConfigurationCategory GetCategory(string key)
    {
        var prefix = key.Split('.')[0].ToLowerInvariant();
        return prefix switch
        {
            "system" => ConfigurationCategory.System,
            "llm" => ConfigurationCategory.Llm,
            "cache" => ConfigurationCategory.Cache,
            "ratelimit" => ConfigurationCategory.RateLimit,
            "webhook" => ConfigurationCategory.Webhook,
            "security" => ConfigurationCategory.Security,
            "storage" => ConfigurationCategory.Storage,
            "search" => ConfigurationCategory.Search,
            "observability" => ConfigurationCategory.Observability,
            _ => ConfigurationCategory.System
        };
    }
}
