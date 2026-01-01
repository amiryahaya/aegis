using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Service for managing and evaluating feature flags
/// </summary>
public interface IFeatureFlagService
{
    /// <summary>
    /// Checks if a feature flag is enabled for the given context
    /// </summary>
    Task<Result<bool>> IsEnabledAsync(
        string flagKey,
        FeatureFlagContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the variant value for a feature flag
    /// </summary>
    Task<Result<T>> GetVariantAsync<T>(
        string flagKey,
        T defaultValue,
        FeatureFlagContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new feature flag
    /// </summary>
    Task<Result<FeatureFlag>> CreateAsync(
        CreateFeatureFlagRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing feature flag
    /// </summary>
    Task<Result<FeatureFlag>> UpdateAsync(
        Guid flagId,
        UpdateFeatureFlagRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a feature flag
    /// </summary>
    Task<Result> DeleteAsync(
        Guid flagId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a feature flag by ID
    /// </summary>
    Task<Result<FeatureFlag>> GetByIdAsync(
        Guid flagId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a feature flag by key
    /// </summary>
    Task<Result<FeatureFlag>> GetByKeyAsync(
        string flagKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all feature flags
    /// </summary>
    Task<Result<IReadOnlyList<FeatureFlag>>> ListAsync(
        FeatureFlagFilter? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets audit history for a feature flag
    /// </summary>
    Task<Result<IReadOnlyList<FeatureFlagAuditEntry>>> GetAuditHistoryAsync(
        Guid flagId,
        int limit = 50,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Context for evaluating feature flags
/// </summary>
public record FeatureFlagContext
{
    public Guid? UserId { get; init; }
    public Guid? TeamId { get; init; }
    public Guid? WorkspaceId { get; init; }
    public string? Environment { get; init; }
    public Dictionary<string, string> Attributes { get; init; } = new();

    public static FeatureFlagContext Empty => new();

    public static FeatureFlagContext ForUser(Guid userId) => new() { UserId = userId };
    public static FeatureFlagContext ForTeam(Guid teamId) => new() { TeamId = teamId };
    public static FeatureFlagContext ForWorkspace(Guid workspaceId) => new() { WorkspaceId = workspaceId };
}

/// <summary>
/// Feature flag definition
/// </summary>
public record FeatureFlag
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string Key { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public FeatureFlagType Type { get; init; } = FeatureFlagType.Boolean;
    public bool IsEnabled { get; init; }
    public object? DefaultValue { get; init; }
    public IReadOnlyList<FeatureFlagRule> Rules { get; init; } = [];
    public IReadOnlyList<FeatureFlagVariant> Variants { get; init; } = [];
    public FeatureFlagStatus Status { get; init; } = FeatureFlagStatus.Active;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; init; }
    public Guid? CreatedBy { get; init; }
    public Guid? UpdatedBy { get; init; }
    public Dictionary<string, string> Tags { get; init; } = new();
}

/// <summary>
/// Types of feature flags
/// </summary>
public enum FeatureFlagType
{
    Boolean,
    String,
    Number,
    Json
}

/// <summary>
/// Status of a feature flag
/// </summary>
public enum FeatureFlagStatus
{
    Active,
    Inactive,
    Archived
}

/// <summary>
/// Rule for evaluating feature flags
/// </summary>
public record FeatureFlagRule
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string Name { get; init; }
    public int Priority { get; init; }
    public IReadOnlyList<FeatureFlagCondition> Conditions { get; init; } = [];
    public FeatureFlagRuleAction Action { get; init; }
    public object? Value { get; init; }
    public string? VariantKey { get; init; }
    public int? RolloutPercentage { get; init; }
}

/// <summary>
/// Condition for a feature flag rule
/// </summary>
public record FeatureFlagCondition
{
    public required string Attribute { get; init; }
    public required FeatureFlagOperator Operator { get; init; }
    public required object Value { get; init; }
}

/// <summary>
/// Operators for feature flag conditions
/// </summary>
public enum FeatureFlagOperator
{
    Equals,
    NotEquals,
    Contains,
    NotContains,
    StartsWith,
    EndsWith,
    GreaterThan,
    LessThan,
    GreaterThanOrEquals,
    LessThanOrEquals,
    In,
    NotIn,
    Matches // Regex
}

/// <summary>
/// Action to take when a rule matches
/// </summary>
public enum FeatureFlagRuleAction
{
    Enable,
    Disable,
    SetValue,
    SetVariant,
    Rollout
}

/// <summary>
/// Variant for a feature flag (A/B testing)
/// </summary>
public record FeatureFlagVariant
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required object Value { get; init; }
    public int Weight { get; init; } = 1;
}

/// <summary>
/// Request to create a feature flag
/// </summary>
public record CreateFeatureFlagRequest
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public FeatureFlagType Type { get; init; } = FeatureFlagType.Boolean;
    public bool IsEnabled { get; init; }
    public object? DefaultValue { get; init; }
    public IReadOnlyList<FeatureFlagRule>? Rules { get; init; }
    public IReadOnlyList<FeatureFlagVariant>? Variants { get; init; }
    public Dictionary<string, string>? Tags { get; init; }
    public Guid? CreatedBy { get; init; }
}

/// <summary>
/// Request to update a feature flag
/// </summary>
public record UpdateFeatureFlagRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public bool? IsEnabled { get; init; }
    public object? DefaultValue { get; init; }
    public IReadOnlyList<FeatureFlagRule>? Rules { get; init; }
    public IReadOnlyList<FeatureFlagVariant>? Variants { get; init; }
    public FeatureFlagStatus? Status { get; init; }
    public Dictionary<string, string>? Tags { get; init; }
    public Guid? UpdatedBy { get; init; }
}

/// <summary>
/// Filter for listing feature flags
/// </summary>
public record FeatureFlagFilter
{
    public FeatureFlagStatus? Status { get; init; }
    public FeatureFlagType? Type { get; init; }
    public bool? IsEnabled { get; init; }
    public string? TagKey { get; init; }
    public string? TagValue { get; init; }
    public string? SearchTerm { get; init; }
}

/// <summary>
/// Audit entry for feature flag changes
/// </summary>
public record FeatureFlagAuditEntry
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid FlagId { get; init; }
    public required string FlagKey { get; init; }
    public required FeatureFlagAuditAction Action { get; init; }
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
    public Guid? ChangedBy { get; init; }
    public DateTime ChangedAt { get; init; } = DateTime.UtcNow;
    public string? Reason { get; init; }
}

/// <summary>
/// Types of audit actions for feature flags
/// </summary>
public enum FeatureFlagAuditAction
{
    Created,
    Updated,
    Enabled,
    Disabled,
    RulesChanged,
    VariantsChanged,
    Archived,
    Deleted
}
