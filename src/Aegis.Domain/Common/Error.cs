namespace Aegis.Domain.Common;

/// <summary>
/// Represents an error with a code and message.
/// </summary>
public record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);
    public static readonly Error NullValue = new("Error.NullValue", "A null value was provided");

    public static Error Validation(string code, string message) => new($"Validation.{code}", message);
    public static Error NotFound(string code, string message) => new($"NotFound.{code}", message);
    public static Error Conflict(string code, string message) => new($"Conflict.{code}", message);
    public static Error Unauthorized(string code, string message) => new($"Unauthorized.{code}", message);
    public static Error Forbidden(string code, string message) => new($"Forbidden.{code}", message);
    public static Error Internal(string code, string message) => new($"Internal.{code}", message);
}

/// <summary>
/// Represents a validation error with multiple error details.
/// </summary>
public record ValidationError : Error
{
    public IReadOnlyCollection<ValidationErrorDetail> Errors { get; }

    public ValidationError(IEnumerable<ValidationErrorDetail> errors)
        : base("Validation.Failed", "One or more validation errors occurred")
    {
        Errors = errors.ToList().AsReadOnly();
    }
}

/// <summary>
/// Represents a single validation error detail.
/// </summary>
public record ValidationErrorDetail(string PropertyName, string ErrorMessage);
