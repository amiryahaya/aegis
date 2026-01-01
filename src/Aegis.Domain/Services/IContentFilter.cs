using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Service for filtering output content to ensure safety and compliance
/// </summary>
public interface IContentFilter
{
    /// <summary>
    /// Filter output content to remove or mask sensitive/inappropriate content
    /// </summary>
    /// <param name="request">The filter request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Filtered content</returns>
    Task<Result<ContentFilterResult>> FilterAsync(
        ContentFilterRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check content for policy violations without modifying it
    /// </summary>
    /// <param name="content">The content to check</param>
    /// <param name="policies">Policies to check against</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Policy check result</returns>
    Task<Result<PolicyCheckResult>> CheckPoliciesAsync(
        string content,
        List<ContentPolicy> policies,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Request for content filtering
/// </summary>
public record ContentFilterRequest
{
    /// <summary>
    /// The content to filter
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Content type for context-aware filtering
    /// </summary>
    public ContentType ContentType { get; init; } = ContentType.Response;

    /// <summary>
    /// Whether to mask PII (personally identifiable information)
    /// </summary>
    public bool MaskPii { get; init; } = true;

    /// <summary>
    /// Whether to filter profanity
    /// </summary>
    public bool FilterProfanity { get; init; } = true;

    /// <summary>
    /// Whether to detect and flag sensitive topics
    /// </summary>
    public bool DetectSensitiveTopics { get; init; } = true;

    /// <summary>
    /// Whether to mask credentials/secrets
    /// </summary>
    public bool MaskCredentials { get; init; } = true;

    /// <summary>
    /// Custom filter rules
    /// </summary>
    public List<FilterRule> CustomRules { get; init; } = new();

    /// <summary>
    /// User's role for role-based filtering
    /// </summary>
    public string? UserRole { get; init; }

    /// <summary>
    /// Workspace ID for workspace-specific filtering
    /// </summary>
    public Guid? WorkspaceId { get; init; }
}

/// <summary>
/// Result of content filtering
/// </summary>
public record ContentFilterResult
{
    /// <summary>
    /// The filtered content
    /// </summary>
    public required string FilteredContent { get; init; }

    /// <summary>
    /// Whether any content was modified
    /// </summary>
    public bool WasModified { get; init; }

    /// <summary>
    /// List of applied filters
    /// </summary>
    public List<AppliedFilter> AppliedFilters { get; init; } = new();

    /// <summary>
    /// Whether content was blocked entirely
    /// </summary>
    public bool WasBlocked { get; init; }

    /// <summary>
    /// Reason for blocking (if blocked)
    /// </summary>
    public string? BlockReason { get; init; }

    /// <summary>
    /// Content safety score (0.0 = unsafe, 1.0 = safe)
    /// </summary>
    public double SafetyScore { get; init; }

    /// <summary>
    /// Detected categories of sensitive content
    /// </summary>
    public List<SensitiveContentCategory> DetectedCategories { get; init; } = new();
}

/// <summary>
/// Types of content being filtered
/// </summary>
public enum ContentType
{
    Response,
    Document,
    Summary,
    Citation,
    Metadata,
    UserMessage
}

/// <summary>
/// Custom filter rule
/// </summary>
public record FilterRule
{
    /// <summary>
    /// Rule identifier
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Pattern to match (regex)
    /// </summary>
    public required string Pattern { get; init; }

    /// <summary>
    /// Action to take when matched
    /// </summary>
    public FilterAction Action { get; init; } = FilterAction.Mask;

    /// <summary>
    /// Replacement text (for mask/replace actions)
    /// </summary>
    public string Replacement { get; init; } = "[REDACTED]";

    /// <summary>
    /// Rule description
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
/// Filter actions
/// </summary>
public enum FilterAction
{
    Mask,
    Remove,
    Replace,
    Flag,
    Block
}

/// <summary>
/// Record of an applied filter
/// </summary>
public record AppliedFilter
{
    /// <summary>
    /// Filter type or rule ID
    /// </summary>
    public required string FilterType { get; init; }

    /// <summary>
    /// Number of matches
    /// </summary>
    public int MatchCount { get; init; }

    /// <summary>
    /// Action taken
    /// </summary>
    public FilterAction Action { get; init; }

    /// <summary>
    /// Description of what was filtered
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
/// Categories of sensitive content
/// </summary>
public enum SensitiveContentCategory
{
    PersonalInfo,
    FinancialInfo,
    HealthInfo,
    Credentials,
    Profanity,
    Violence,
    HateSpeech,
    SexualContent,
    PoliticalContent,
    ReligiousContent,
    LegalContent,
    ProprietaryInfo,
    Other
}

/// <summary>
/// Content policy for checking
/// </summary>
public record ContentPolicy
{
    /// <summary>
    /// Policy identifier
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Policy name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Categories this policy covers
    /// </summary>
    public List<SensitiveContentCategory> Categories { get; init; } = new();

    /// <summary>
    /// Severity threshold (0.0 - 1.0)
    /// </summary>
    public double SeverityThreshold { get; init; } = 0.5;

    /// <summary>
    /// Action to take on violation
    /// </summary>
    public PolicyAction Action { get; init; } = PolicyAction.Warn;
}

/// <summary>
/// Policy violation actions
/// </summary>
public enum PolicyAction
{
    Allow,
    Warn,
    Block,
    Escalate
}

/// <summary>
/// Result of policy checking
/// </summary>
public record PolicyCheckResult
{
    /// <summary>
    /// Whether all policies passed
    /// </summary>
    public bool Passed { get; init; }

    /// <summary>
    /// List of policy violations
    /// </summary>
    public List<PolicyViolation> Violations { get; init; } = new();

    /// <summary>
    /// Overall risk level
    /// </summary>
    public RiskLevel RiskLevel { get; init; }

    /// <summary>
    /// Recommended action
    /// </summary>
    public PolicyAction RecommendedAction { get; init; }
}

/// <summary>
/// Policy violation details
/// </summary>
public record PolicyViolation
{
    /// <summary>
    /// Policy that was violated
    /// </summary>
    public required string PolicyId { get; init; }

    /// <summary>
    /// Violation description
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Severity score
    /// </summary>
    public double Severity { get; init; }

    /// <summary>
    /// Category of violation
    /// </summary>
    public SensitiveContentCategory Category { get; init; }

    /// <summary>
    /// Location in content
    /// </summary>
    public string? Location { get; init; }
}

/// <summary>
/// Risk levels
/// </summary>
public enum RiskLevel
{
    None,
    Low,
    Medium,
    High,
    Critical
}
