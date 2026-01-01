using System.Text.RegularExpressions;
using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Security;

/// <summary>
/// Service for filtering output content to ensure safety and compliance
/// </summary>
public class ContentFilter : IContentFilter
{
    private readonly ILogger<ContentFilter> _logger;

    // PII patterns
    private static readonly Dictionary<string, (string Pattern, string Replacement)> PiiPatterns = new()
    {
        ["PII_EMAIL"] = (@"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}", "[EMAIL]"),
        ["PII_PHONE"] = (@"(\+?1[-.\s]?)?\(?\d{3}\)?[-.\s]?\d{3}[-.\s]?\d{4}", "[PHONE]"),
        ["PII_SSN"] = (@"\b\d{3}[-\s]?\d{2}[-\s]?\d{4}\b", "[SSN]"),
        ["PII_CREDIT_CARD"] = (@"\b(?:\d{4}[-\s]?){3}\d{4}\b", "[CREDIT_CARD]"),
        ["PII_IP_ADDRESS"] = (@"\b(?:\d{1,3}\.){3}\d{1,3}\b", "[IP_ADDRESS]")
    };

    // Credential patterns
    private static readonly Dictionary<string, (string Pattern, string Replacement)> CredentialPatterns = new()
    {
        ["API_KEY"] = (@"(?i)(api[_-]?key|apikey|api_secret|secret_key)\s*[=:]\s*['""]?([a-zA-Z0-9_\-]{20,})['""]?", "[API_KEY]"),
        ["PASSWORD"] = (@"(?i)(password|passwd|pwd)\s*[=:]\s*['""]?([^\s'"",]{6,})['""]?", "[PASSWORD]"),
        ["TOKEN"] = (@"(?i)(token|bearer|auth)\s*[=:]\s*['""]?([a-zA-Z0-9_\-\.]{20,})['""]?", "[TOKEN]"),
        ["PRIVATE_KEY"] = (@"-----BEGIN\s+(?:RSA\s+)?PRIVATE\s+KEY-----[\s\S]*?-----END\s+(?:RSA\s+)?PRIVATE\s+KEY-----", "[PRIVATE_KEY]"),
        ["AWS_KEY"] = (@"(?i)AKIA[0-9A-Z]{16}", "[AWS_KEY]"),
        ["CONNECTION_STRING"] = (@"(?i)(Password|Pwd)\s*=\s*([^;]+)", "Password=[REDACTED]")
    };

    // Profanity list (basic - would be more comprehensive in production)
    private static readonly HashSet<string> ProfanityWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "damn", "hell", "crap", "shit", "fuck", "ass", "bitch"
    };

    public ContentFilter(ILogger<ContentFilter> logger)
    {
        _logger = logger;
    }

    public async Task<Result<ContentFilterResult>> FilterAsync(
        ContentFilterRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (request.Content == null)
            {
                return Result<ContentFilterResult>.Failure(
                    Error.Validation("ContentFilter.NullContent", "Content cannot be null"));
            }

            var filtered = request.Content;
            var wasModified = false;
            var wasBlocked = false;
            string? blockReason = null;
            var appliedFilters = new List<AppliedFilter>();
            var detectedCategories = new List<SensitiveContentCategory>();
            var safetyScore = 1.0;

            // Apply PII masking
            if (request.MaskPii)
            {
                foreach (var (filterType, (pattern, replacement)) in PiiPatterns)
                {
                    var regex = new Regex(pattern, RegexOptions.IgnoreCase);
                    var matches = regex.Matches(filtered);

                    if (matches.Count > 0)
                    {
                        filtered = regex.Replace(filtered, replacement);
                        wasModified = true;
                        appliedFilters.Add(new AppliedFilter
                        {
                            FilterType = filterType,
                            MatchCount = matches.Count,
                            Action = FilterAction.Mask,
                            Description = $"Masked {matches.Count} {filterType} occurrence(s)"
                        });
                        detectedCategories.Add(SensitiveContentCategory.PersonalInfo);
                        safetyScore -= 0.05 * matches.Count;
                    }
                }
            }

            // Apply credential masking
            if (request.MaskCredentials)
            {
                foreach (var (filterType, (pattern, replacement)) in CredentialPatterns)
                {
                    var regex = new Regex(pattern, RegexOptions.IgnoreCase);
                    var matches = regex.Matches(filtered);

                    if (matches.Count > 0)
                    {
                        filtered = regex.Replace(filtered, replacement);
                        wasModified = true;
                        appliedFilters.Add(new AppliedFilter
                        {
                            FilterType = filterType,
                            MatchCount = matches.Count,
                            Action = FilterAction.Mask,
                            Description = $"Masked {matches.Count} credential(s)"
                        });
                        detectedCategories.Add(SensitiveContentCategory.Credentials);
                        safetyScore -= 0.1 * matches.Count;
                    }
                }
            }

            // Apply profanity filtering
            if (request.FilterProfanity)
            {
                var profanityCount = 0;
                foreach (var word in ProfanityWords)
                {
                    var pattern = $@"\b{Regex.Escape(word)}\b";
                    var regex = new Regex(pattern, RegexOptions.IgnoreCase);
                    var matches = regex.Matches(filtered);

                    if (matches.Count > 0)
                    {
                        profanityCount += matches.Count;
                        filtered = regex.Replace(filtered, new string('*', word.Length));
                        wasModified = true;
                    }
                }

                if (profanityCount > 0)
                {
                    appliedFilters.Add(new AppliedFilter
                    {
                        FilterType = "PROFANITY",
                        MatchCount = profanityCount,
                        Action = FilterAction.Mask,
                        Description = $"Masked {profanityCount} profanity occurrence(s)"
                    });
                    detectedCategories.Add(SensitiveContentCategory.Profanity);
                    safetyScore -= 0.05 * profanityCount;
                }
            }

            // Apply custom rules
            foreach (var rule in request.CustomRules)
            {
                var regex = new Regex(rule.Pattern, RegexOptions.IgnoreCase);
                var matches = regex.Matches(filtered);

                if (matches.Count > 0)
                {
                    switch (rule.Action)
                    {
                        case FilterAction.Block:
                            wasBlocked = true;
                            blockReason = $"Content blocked by rule: {rule.Id}";
                            break;

                        case FilterAction.Mask:
                            filtered = regex.Replace(filtered, rule.Replacement);
                            wasModified = true;
                            break;

                        case FilterAction.Remove:
                            filtered = regex.Replace(filtered, "");
                            wasModified = true;
                            break;

                        case FilterAction.Replace:
                            filtered = regex.Replace(filtered, rule.Replacement);
                            wasModified = true;
                            break;

                        case FilterAction.Flag:
                            // Just flag, don't modify
                            break;
                    }

                    appliedFilters.Add(new AppliedFilter
                    {
                        FilterType = rule.Id,
                        MatchCount = matches.Count,
                        Action = rule.Action,
                        Description = rule.Description ?? $"Custom rule {rule.Id} applied"
                    });
                }
            }

            // Ensure safety score is within bounds
            safetyScore = Math.Max(0, Math.Min(1, safetyScore));

            _logger.LogDebug(
                "Content filtered: modified={Modified}, blocked={Blocked}, filters={FilterCount}, safetyScore={SafetyScore}",
                wasModified, wasBlocked, appliedFilters.Count, safetyScore);

            return Result<ContentFilterResult>.Success(new ContentFilterResult
            {
                FilteredContent = wasBlocked ? "" : filtered,
                WasModified = wasModified,
                AppliedFilters = appliedFilters,
                WasBlocked = wasBlocked,
                BlockReason = blockReason,
                SafetyScore = safetyScore,
                DetectedCategories = detectedCategories.Distinct().ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error filtering content");
            return Result<ContentFilterResult>.Failure(
                Error.Internal("ContentFilter.Error", ex.Message));
        }
    }

    public async Task<Result<PolicyCheckResult>> CheckPoliciesAsync(
        string content,
        List<ContentPolicy> policies,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var violations = new List<PolicyViolation>();
            var recommendedAction = PolicyAction.Allow;
            var riskLevel = RiskLevel.None;

            foreach (var policy in policies)
            {
                foreach (var category in policy.Categories)
                {
                    var detected = DetectCategory(content, category);

                    if (detected.severity > policy.SeverityThreshold)
                    {
                        violations.Add(new PolicyViolation
                        {
                            PolicyId = policy.Id,
                            Description = $"Policy '{policy.Name}' violated: {detected.description}",
                            Severity = detected.severity,
                            Category = category,
                            Location = detected.location
                        });

                        // Update recommended action to the most severe
                        if (policy.Action > recommendedAction)
                        {
                            recommendedAction = policy.Action;
                        }
                    }
                }
            }

            // Determine risk level
            if (violations.Count == 0)
            {
                riskLevel = RiskLevel.None;
            }
            else
            {
                var maxSeverity = violations.Max(v => v.Severity);
                riskLevel = maxSeverity switch
                {
                    < 0.3 => RiskLevel.Low,
                    < 0.6 => RiskLevel.Medium,
                    < 0.9 => RiskLevel.High,
                    _ => RiskLevel.Critical
                };
            }

            return Result<PolicyCheckResult>.Success(new PolicyCheckResult
            {
                Passed = violations.Count == 0,
                Violations = violations,
                RiskLevel = riskLevel,
                RecommendedAction = recommendedAction
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking policies");
            return Result<PolicyCheckResult>.Failure(
                Error.Internal("PolicyCheck.Error", ex.Message));
        }
    }

    private (double severity, string description, string? location) DetectCategory(string content, SensitiveContentCategory category)
    {
        return category switch
        {
            SensitiveContentCategory.PersonalInfo => DetectPii(content),
            SensitiveContentCategory.Credentials => DetectCredentials(content),
            SensitiveContentCategory.Profanity => DetectProfanity(content),
            SensitiveContentCategory.FinancialInfo => DetectFinancialInfo(content),
            _ => (0.0, "No detection", null)
        };
    }

    private (double severity, string description, string? location) DetectPii(string content)
    {
        var totalMatches = 0;
        string? firstLocation = null;

        foreach (var (_, (pattern, _)) in PiiPatterns)
        {
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);
            var matches = regex.Matches(content);
            totalMatches += matches.Count;

            if (matches.Count > 0 && firstLocation == null)
            {
                firstLocation = $"position {matches[0].Index}";
            }
        }

        if (totalMatches == 0) return (0.0, "No PII detected", null);

        return (Math.Min(1.0, totalMatches * 0.2), $"Found {totalMatches} PII patterns", firstLocation);
    }

    private (double severity, string description, string? location) DetectCredentials(string content)
    {
        var totalMatches = 0;
        string? firstLocation = null;

        foreach (var (_, (pattern, _)) in CredentialPatterns)
        {
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);
            var matches = regex.Matches(content);
            totalMatches += matches.Count;

            if (matches.Count > 0 && firstLocation == null)
            {
                firstLocation = $"position {matches[0].Index}";
            }
        }

        if (totalMatches == 0) return (0.0, "No credentials detected", null);

        return (Math.Min(1.0, totalMatches * 0.4), $"Found {totalMatches} credential patterns", firstLocation);
    }

    private (double severity, string description, string? location) DetectProfanity(string content)
    {
        var totalMatches = 0;
        string? firstLocation = null;

        foreach (var word in ProfanityWords)
        {
            var pattern = $@"\b{Regex.Escape(word)}\b";
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);
            var matches = regex.Matches(content);
            totalMatches += matches.Count;

            if (matches.Count > 0 && firstLocation == null)
            {
                firstLocation = $"position {matches[0].Index}";
            }
        }

        if (totalMatches == 0) return (0.0, "No profanity detected", null);

        return (Math.Min(1.0, totalMatches * 0.15), $"Found {totalMatches} profanity instances", firstLocation);
    }

    private (double severity, string description, string? location) DetectFinancialInfo(string content)
    {
        // Check for credit card numbers and financial patterns
        var ccPattern = @"\b(?:\d{4}[-\s]?){3}\d{4}\b";
        var regex = new Regex(ccPattern);
        var matches = regex.Matches(content);

        if (matches.Count == 0) return (0.0, "No financial info detected", null);

        return (Math.Min(1.0, matches.Count * 0.3), $"Found {matches.Count} financial patterns", $"position {matches[0].Index}");
    }
}
