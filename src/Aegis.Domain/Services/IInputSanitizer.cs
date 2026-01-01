using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Service for sanitizing and validating user input to prevent security vulnerabilities
/// </summary>
public interface IInputSanitizer
{
    /// <summary>
    /// Sanitize user input to remove potentially dangerous content
    /// </summary>
    /// <param name="request">The sanitization request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Sanitized content</returns>
    Task<Result<SanitizationResult>> SanitizeAsync(
        SanitizationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate input against security rules without modifying it
    /// </summary>
    /// <param name="input">The input to validate</param>
    /// <param name="context">Validation context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result</returns>
    Task<Result<ValidationResult>> ValidateAsync(
        string input,
        ValidationContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Request for input sanitization
/// </summary>
public record SanitizationRequest
{
    /// <summary>
    /// The raw input to sanitize
    /// </summary>
    public required string Input { get; init; }

    /// <summary>
    /// The type of input (query, document, metadata, etc.)
    /// </summary>
    public InputType InputType { get; init; } = InputType.Query;

    /// <summary>
    /// Whether to strip HTML tags
    /// </summary>
    public bool StripHtml { get; init; } = true;

    /// <summary>
    /// Whether to encode special characters
    /// </summary>
    public bool EncodeSpecialChars { get; init; } = true;

    /// <summary>
    /// Whether to detect and block prompt injection attempts
    /// </summary>
    public bool DetectPromptInjection { get; init; } = true;

    /// <summary>
    /// Maximum allowed length (0 = no limit)
    /// </summary>
    public int MaxLength { get; init; } = 10000;

    /// <summary>
    /// User ID for audit logging
    /// </summary>
    public Guid? UserId { get; init; }
}

/// <summary>
/// Result of input sanitization
/// </summary>
public record SanitizationResult
{
    /// <summary>
    /// The sanitized content
    /// </summary>
    public required string SanitizedContent { get; init; }

    /// <summary>
    /// Original content length
    /// </summary>
    public int OriginalLength { get; init; }

    /// <summary>
    /// Sanitized content length
    /// </summary>
    public int SanitizedLength { get; init; }

    /// <summary>
    /// Whether any modifications were made
    /// </summary>
    public bool WasModified { get; init; }

    /// <summary>
    /// List of detected threats that were neutralized
    /// </summary>
    public List<DetectedThreat> DetectedThreats { get; init; } = new();

    /// <summary>
    /// Whether the input was truncated due to length
    /// </summary>
    public bool WasTruncated { get; init; }
}

/// <summary>
/// Represents a detected security threat
/// </summary>
public record DetectedThreat
{
    /// <summary>
    /// Type of threat detected
    /// </summary>
    public required ThreatType Type { get; init; }

    /// <summary>
    /// Description of the threat
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Severity level (1-10)
    /// </summary>
    public int Severity { get; init; }

    /// <summary>
    /// Position in the original input where threat was detected
    /// </summary>
    public int Position { get; init; }

    /// <summary>
    /// The matched pattern or content
    /// </summary>
    public string? MatchedContent { get; init; }
}

/// <summary>
/// Types of security threats
/// </summary>
public enum ThreatType
{
    PromptInjection,
    SqlInjection,
    XssAttack,
    CommandInjection,
    PathTraversal,
    MaliciousUrl,
    SensitiveDataExposure,
    ExcessiveLength,
    EncodingAttack,
    Other
}

/// <summary>
/// Types of input being sanitized
/// </summary>
public enum InputType
{
    Query,
    Document,
    Metadata,
    FileName,
    Url,
    ApiParameter
}

/// <summary>
/// Context for input validation
/// </summary>
public record ValidationContext
{
    /// <summary>
    /// Type of input being validated
    /// </summary>
    public InputType InputType { get; init; } = InputType.Query;

    /// <summary>
    /// Whether to allow HTML content
    /// </summary>
    public bool AllowHtml { get; init; } = false;

    /// <summary>
    /// Whether to allow URLs
    /// </summary>
    public bool AllowUrls { get; init; } = true;

    /// <summary>
    /// Allowed URL schemes (http, https, etc.)
    /// </summary>
    public List<string> AllowedUrlSchemes { get; init; } = new() { "http", "https" };

    /// <summary>
    /// Maximum allowed length
    /// </summary>
    public int MaxLength { get; init; } = 10000;

    /// <summary>
    /// Minimum required length
    /// </summary>
    public int MinLength { get; init; } = 1;

    /// <summary>
    /// Custom validation rules
    /// </summary>
    public List<string> CustomRules { get; init; } = new();
}

/// <summary>
/// Result of input validation
/// </summary>
public record ValidationResult
{
    /// <summary>
    /// Whether the input is valid
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// List of validation errors
    /// </summary>
    public List<ValidationError> Errors { get; init; } = new();

    /// <summary>
    /// List of validation warnings (non-blocking)
    /// </summary>
    public List<ValidationWarning> Warnings { get; init; } = new();

    /// <summary>
    /// Risk score (0.0 - 1.0)
    /// </summary>
    public double RiskScore { get; init; }
}

/// <summary>
/// Validation error
/// </summary>
public record ValidationError
{
    /// <summary>
    /// Error code
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Error message
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Field or position where error occurred
    /// </summary>
    public string? Location { get; init; }
}

/// <summary>
/// Validation warning
/// </summary>
public record ValidationWarning
{
    /// <summary>
    /// Warning code
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Warning message
    /// </summary>
    public required string Message { get; init; }
}
