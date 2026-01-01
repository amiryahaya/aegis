using System.Text.RegularExpressions;
using System.Web;
using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Security;

/// <summary>
/// Service for sanitizing and validating user input
/// </summary>
public class InputSanitizer : IInputSanitizer
{
    private readonly ILogger<InputSanitizer> _logger;

    // Prompt injection patterns
    private static readonly string[] PromptInjectionPatterns = new[]
    {
        @"ignore\s+(all\s+)?(previous|prior|above)\s+(instructions|prompts|commands)",
        @"disregard\s+(all\s+)?(previous|prior|above)",
        @"forget\s+(everything|all)\s+(you|that)",
        @"you\s+are\s+now\s+a",
        @"new\s+instructions?:",
        @"SYSTEM\s*:",
        @"USER\s*:",
        @"ASSISTANT\s*:",
        @"<\|im_start\|>",
        @"<\|im_end\|>",
        @"DAN\s+mode",
        @"jailbreak",
        @"pretend\s+you\s+(are|can)",
        @"act\s+as\s+(if|a)",
        @"roleplay\s+as",
        @"bypass\s+(your|the)\s+(safety|restrictions|limitations)"
    };

    // SQL injection patterns
    private static readonly string[] SqlInjectionPatterns = new[]
    {
        @"';\s*DROP\s+TABLE",
        @"';\s*DELETE\s+FROM",
        @"';\s*UPDATE\s+\w+\s+SET",
        @"';\s*INSERT\s+INTO",
        @"UNION\s+SELECT",
        @"UNION\s+ALL\s+SELECT",
        @"OR\s+1\s*=\s*1",
        @"AND\s+1\s*=\s*1",
        @"--\s*$",
        @"/\*.*\*/",
        @";\s*EXEC\s+",
        @";\s*EXECUTE\s+",
        @"xp_cmdshell",
        @"INFORMATION_SCHEMA"
    };

    // XSS patterns
    private static readonly string[] XssPatterns = new[]
    {
        @"<script[^>]*>",
        @"</script>",
        @"javascript\s*:",
        @"on\w+\s*=",
        @"<iframe",
        @"<object",
        @"<embed",
        @"<form",
        @"<input",
        @"<img[^>]*\s+onerror",
        @"expression\s*\(",
        @"url\s*\(\s*['""]?\s*javascript"
    };

    // Path traversal patterns
    private static readonly string[] PathTraversalPatterns = new[]
    {
        @"\.\./",
        @"\.\.\\",
        @"%2e%2e%2f",
        @"%2e%2e/",
        @"\.\.%2f",
        @"%2e%2e\\",
        @"/etc/passwd",
        @"/etc/shadow",
        @"C:\\Windows",
        @"\\\\[^\\]+"
    };

    public InputSanitizer(ILogger<InputSanitizer> logger)
    {
        _logger = logger;
    }

    public async Task<Result<SanitizationResult>> SanitizeAsync(
        SanitizationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (request.Input == null)
            {
                return Result<SanitizationResult>.Failure(
                    Error.Validation("Sanitization.NullInput", "Input cannot be null"));
            }

            var sanitized = request.Input;
            var wasModified = false;
            var wasTruncated = false;
            var detectedThreats = new List<DetectedThreat>();

            // Trim whitespace
            sanitized = sanitized.Trim();
            if (sanitized != request.Input.Trim())
            {
                wasModified = true;
            }

            // Check for excessive length
            if (request.MaxLength > 0 && sanitized.Length > request.MaxLength)
            {
                detectedThreats.Add(new DetectedThreat
                {
                    Type = ThreatType.ExcessiveLength,
                    Description = $"Input exceeded maximum length of {request.MaxLength}",
                    Severity = 3,
                    Position = request.MaxLength
                });
                sanitized = sanitized.Substring(0, request.MaxLength);
                wasModified = true;
                wasTruncated = true;
            }

            // Detect and handle XSS/HTML
            if (request.StripHtml)
            {
                var xssThreats = DetectPatterns(sanitized, XssPatterns, ThreatType.XssAttack, "XSS attack pattern");
                detectedThreats.AddRange(xssThreats);

                if (xssThreats.Count > 0)
                {
                    sanitized = StripHtmlTags(sanitized);
                    wasModified = true;
                }
            }

            // Detect prompt injection
            if (request.DetectPromptInjection)
            {
                var promptThreats = DetectPatterns(sanitized, PromptInjectionPatterns, ThreatType.PromptInjection, "Prompt injection attempt");
                detectedThreats.AddRange(promptThreats);

                if (promptThreats.Count > 0)
                {
                    sanitized = NeutralizePromptInjection(sanitized);
                    wasModified = true;
                }
            }

            // Detect SQL injection
            var sqlThreats = DetectPatterns(sanitized, SqlInjectionPatterns, ThreatType.SqlInjection, "SQL injection attempt");
            detectedThreats.AddRange(sqlThreats);

            if (sqlThreats.Count > 0)
            {
                sanitized = NeutralizeSqlInjection(sanitized);
                wasModified = true;
            }

            // Detect path traversal for file-related inputs
            if (request.InputType == InputType.FileName || request.InputType == InputType.Url)
            {
                var pathThreats = DetectPatterns(sanitized, PathTraversalPatterns, ThreatType.PathTraversal, "Path traversal attempt");
                detectedThreats.AddRange(pathThreats);

                if (pathThreats.Count > 0)
                {
                    sanitized = NeutralizePathTraversal(sanitized);
                    wasModified = true;
                }
            }

            // Encode special characters if requested
            if (request.EncodeSpecialChars && ContainsSpecialChars(sanitized))
            {
                sanitized = EncodeSpecialCharacters(sanitized);
                wasModified = true;
            }

            _logger.LogDebug(
                "Sanitized input: modified={Modified}, threats={ThreatCount}, originalLength={Original}, sanitizedLength={Sanitized}",
                wasModified, detectedThreats.Count, request.Input.Length, sanitized.Length);

            return Result<SanitizationResult>.Success(new SanitizationResult
            {
                SanitizedContent = sanitized,
                OriginalLength = request.Input.Length,
                SanitizedLength = sanitized.Length,
                WasModified = wasModified,
                DetectedThreats = detectedThreats,
                WasTruncated = wasTruncated
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sanitizing input");
            return Result<SanitizationResult>.Failure(
                Error.Internal("Sanitization.Error", ex.Message));
        }
    }

    public async Task<Result<ValidationResult>> ValidateAsync(
        string input,
        ValidationContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var errors = new List<Aegis.Domain.Services.ValidationError>();
            var warnings = new List<Aegis.Domain.Services.ValidationWarning>();
            var riskScore = 0.0;

            // Length validation
            if (input.Length < context.MinLength)
            {
                errors.Add(new Aegis.Domain.Services.ValidationError
                {
                    Code = "MinLength",
                    Message = $"Input must be at least {context.MinLength} characters",
                    Location = "input"
                });
            }

            if (input.Length > context.MaxLength)
            {
                errors.Add(new Aegis.Domain.Services.ValidationError
                {
                    Code = "MaxLength",
                    Message = $"Input must not exceed {context.MaxLength} characters",
                    Location = "input"
                });
            }

            // HTML validation
            if (!context.AllowHtml && ContainsHtml(input))
            {
                warnings.Add(new Aegis.Domain.Services.ValidationWarning
                {
                    Code = "HtmlDetected",
                    Message = "HTML tags detected in input"
                });
                riskScore += 0.1;
            }

            // URL validation
            if (ContainsMaliciousUrl(input, context.AllowedUrlSchemes))
            {
                warnings.Add(new Aegis.Domain.Services.ValidationWarning
                {
                    Code = "MaliciousUrl",
                    Message = "Potentially malicious URL scheme detected"
                });
                riskScore += 0.3;
            }

            // Check for injection patterns
            if (DetectPatterns(input, PromptInjectionPatterns, ThreatType.PromptInjection, "").Count > 0)
            {
                warnings.Add(new Aegis.Domain.Services.ValidationWarning
                {
                    Code = "PromptInjection",
                    Message = "Potential prompt injection detected"
                });
                riskScore += 0.4;
            }

            if (DetectPatterns(input, SqlInjectionPatterns, ThreatType.SqlInjection, "").Count > 0)
            {
                warnings.Add(new Aegis.Domain.Services.ValidationWarning
                {
                    Code = "SqlInjection",
                    Message = "Potential SQL injection detected"
                });
                riskScore += 0.5;
            }

            return Result<ValidationResult>.Success(new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors,
                Warnings = warnings,
                RiskScore = Math.Min(riskScore, 1.0)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating input");
            return Result<ValidationResult>.Failure(
                Error.Internal("Validation.Error", ex.Message));
        }
    }

    private List<DetectedThreat> DetectPatterns(string input, string[] patterns, ThreatType threatType, string description)
    {
        var threats = new List<DetectedThreat>();

        foreach (var pattern in patterns)
        {
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);
            var matches = regex.Matches(input);

            foreach (Match match in matches)
            {
                threats.Add(new DetectedThreat
                {
                    Type = threatType,
                    Description = description,
                    Severity = GetSeverity(threatType),
                    Position = match.Index,
                    MatchedContent = match.Value.Length > 50 ? match.Value.Substring(0, 50) + "..." : match.Value
                });
            }
        }

        return threats;
    }

    private int GetSeverity(ThreatType threatType) => threatType switch
    {
        ThreatType.PromptInjection => 8,
        ThreatType.SqlInjection => 9,
        ThreatType.XssAttack => 7,
        ThreatType.CommandInjection => 10,
        ThreatType.PathTraversal => 6,
        ThreatType.MaliciousUrl => 5,
        ThreatType.ExcessiveLength => 3,
        _ => 5
    };

    private string StripHtmlTags(string input)
    {
        // Remove script tags and their content
        var result = Regex.Replace(input, @"<script[^>]*>[\s\S]*?</script>", "", RegexOptions.IgnoreCase);
        // Remove style tags and their content
        result = Regex.Replace(result, @"<style[^>]*>[\s\S]*?</style>", "", RegexOptions.IgnoreCase);
        // Remove all other HTML tags
        result = Regex.Replace(result, @"<[^>]+>", "");
        return result.Trim();
    }

    private string NeutralizePromptInjection(string input)
    {
        var result = input;
        foreach (var pattern in PromptInjectionPatterns)
        {
            result = Regex.Replace(result, pattern, "[FILTERED]", RegexOptions.IgnoreCase);
        }
        return result;
    }

    private string NeutralizeSqlInjection(string input)
    {
        var result = input;
        // Escape single quotes
        result = result.Replace("'", "''");
        // Remove comment markers
        result = Regex.Replace(result, @"--\s*$", "", RegexOptions.Multiline);
        result = Regex.Replace(result, @"/\*.*?\*/", "", RegexOptions.Singleline);
        return result;
    }

    private string NeutralizePathTraversal(string input)
    {
        var result = input;
        // Remove path traversal sequences
        result = Regex.Replace(result, @"\.\.[\\/]", "", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"%2e%2e[%2f%5c]", "", RegexOptions.IgnoreCase);
        // URL decode first, then remove
        result = HttpUtility.UrlDecode(result);
        result = Regex.Replace(result, @"\.\.[\\/]", "");
        return result;
    }

    private bool ContainsSpecialChars(string input)
    {
        return input.Any(c => c == '<' || c == '>' || c == '&' || c == '"' || c == '\'');
    }

    private string EncodeSpecialCharacters(string input)
    {
        return HttpUtility.HtmlEncode(input);
    }

    private bool ContainsHtml(string input)
    {
        return Regex.IsMatch(input, @"<[a-zA-Z][^>]*>", RegexOptions.IgnoreCase);
    }

    private bool ContainsMaliciousUrl(string input, List<string> allowedSchemes)
    {
        // Check for javascript: and other dangerous schemes
        var dangerousSchemes = new[] { "javascript:", "data:", "vbscript:", "file:" };
        var inputLower = input.ToLowerInvariant();

        foreach (var scheme in dangerousSchemes)
        {
            if (inputLower.Contains(scheme) && !allowedSchemes.Contains(scheme.TrimEnd(':')))
            {
                return true;
            }
        }

        return false;
    }
}
