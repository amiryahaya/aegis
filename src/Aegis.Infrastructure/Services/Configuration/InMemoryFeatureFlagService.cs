using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Configuration;

/// <summary>
/// In-memory implementation of feature flag service
/// </summary>
public class InMemoryFeatureFlagService : IFeatureFlagService
{
    private readonly ConcurrentDictionary<Guid, FeatureFlag> _flags = new();
    private readonly ConcurrentDictionary<string, Guid> _keyToIdMap = new();
    private readonly ConcurrentDictionary<Guid, List<FeatureFlagAuditEntry>> _auditHistory = new();
    private readonly ILogger<InMemoryFeatureFlagService> _logger;

    public InMemoryFeatureFlagService(ILogger<InMemoryFeatureFlagService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<Result<bool>> IsEnabledAsync(
        string flagKey,
        FeatureFlagContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(flagKey);
        ArgumentNullException.ThrowIfNull(context);

        if (!_keyToIdMap.TryGetValue(flagKey, out var flagId) ||
            !_flags.TryGetValue(flagId, out var flag))
        {
            return Task.FromResult(Result<bool>.Success(false));
        }

        if (flag.Status != FeatureFlagStatus.Active)
        {
            return Task.FromResult(Result<bool>.Success(false));
        }

        var result = EvaluateFlag(flag, context);
        return Task.FromResult(Result<bool>.Success(result is bool b && b));
    }

    public Task<Result<T>> GetVariantAsync<T>(
        string flagKey,
        T defaultValue,
        FeatureFlagContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(flagKey);
        ArgumentNullException.ThrowIfNull(context);

        if (!_keyToIdMap.TryGetValue(flagKey, out var flagId) ||
            !_flags.TryGetValue(flagId, out var flag))
        {
            return Task.FromResult(Result<T>.Success(defaultValue));
        }

        if (flag.Status != FeatureFlagStatus.Active)
        {
            return Task.FromResult(Result<T>.Success(defaultValue));
        }

        var result = EvaluateFlag(flag, context);

        if (result == null)
        {
            return Task.FromResult(Result<T>.Success(defaultValue));
        }

        try
        {
            if (result is T typedResult)
            {
                return Task.FromResult(Result<T>.Success(typedResult));
            }

            // Try to convert
            if (result is JsonElement jsonElement)
            {
                var converted = JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
                return Task.FromResult(Result<T>.Success(converted ?? defaultValue));
            }

            var convertedValue = (T)Convert.ChangeType(result, typeof(T));
            return Task.FromResult(Result<T>.Success(convertedValue));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to convert flag {FlagKey} value to {Type}", flagKey, typeof(T).Name);
            return Task.FromResult(Result<T>.Success(defaultValue));
        }
    }

    public Task<Result<FeatureFlag>> CreateAsync(
        CreateFeatureFlagRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Key))
        {
            return Task.FromResult(Result<FeatureFlag>.Failure(
                Error.Validation("FeatureFlag.InvalidKey", "Flag key is required")));
        }

        if (!IsValidFlagKey(request.Key))
        {
            return Task.FromResult(Result<FeatureFlag>.Failure(
                Error.Validation("FeatureFlag.InvalidKey",
                    "Flag key must be alphanumeric with hyphens or underscores")));
        }

        if (_keyToIdMap.ContainsKey(request.Key))
        {
            return Task.FromResult(Result<FeatureFlag>.Failure(
                Error.Conflict("FeatureFlag.KeyExists", $"Flag with key '{request.Key}' already exists")));
        }

        var flag = new FeatureFlag
        {
            Key = request.Key,
            Name = request.Name,
            Description = request.Description,
            Type = request.Type,
            IsEnabled = request.IsEnabled,
            DefaultValue = request.DefaultValue ?? GetDefaultValueForType(request.Type),
            Rules = request.Rules ?? [],
            Variants = request.Variants ?? [],
            Tags = request.Tags ?? new Dictionary<string, string>(),
            CreatedBy = request.CreatedBy
        };

        _flags[flag.Id] = flag;
        _keyToIdMap[flag.Key] = flag.Id;

        AddAuditEntry(flag.Id, flag.Key, FeatureFlagAuditAction.Created, null,
            JsonSerializer.Serialize(flag), request.CreatedBy);

        _logger.LogInformation("Created feature flag {FlagKey} with ID {FlagId}", flag.Key, flag.Id);

        return Task.FromResult(Result<FeatureFlag>.Success(flag));
    }

    public Task<Result<FeatureFlag>> UpdateAsync(
        Guid flagId,
        UpdateFeatureFlagRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_flags.TryGetValue(flagId, out var existing))
        {
            return Task.FromResult(Result<FeatureFlag>.Failure(
                Error.NotFound("FeatureFlag.NotFound", $"Flag {flagId} not found")));
        }

        var oldValue = JsonSerializer.Serialize(existing);

        var updated = existing with
        {
            Name = request.Name ?? existing.Name,
            Description = request.Description ?? existing.Description,
            IsEnabled = request.IsEnabled ?? existing.IsEnabled,
            DefaultValue = request.DefaultValue ?? existing.DefaultValue,
            Rules = request.Rules ?? existing.Rules,
            Variants = request.Variants ?? existing.Variants,
            Status = request.Status ?? existing.Status,
            Tags = request.Tags ?? existing.Tags,
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = request.UpdatedBy
        };

        _flags[flagId] = updated;

        var action = DetermineAuditAction(existing, updated);
        AddAuditEntry(flagId, existing.Key, action, oldValue,
            JsonSerializer.Serialize(updated), request.UpdatedBy);

        _logger.LogInformation("Updated feature flag {FlagKey}", existing.Key);

        return Task.FromResult(Result<FeatureFlag>.Success(updated));
    }

    public Task<Result> DeleteAsync(
        Guid flagId,
        CancellationToken cancellationToken = default)
    {
        if (!_flags.TryRemove(flagId, out var flag))
        {
            return Task.FromResult(Result.Failure(
                Error.NotFound("FeatureFlag.NotFound", $"Flag {flagId} not found")));
        }

        _keyToIdMap.TryRemove(flag.Key, out _);

        AddAuditEntry(flagId, flag.Key, FeatureFlagAuditAction.Deleted,
            JsonSerializer.Serialize(flag), null, null);

        _logger.LogInformation("Deleted feature flag {FlagKey}", flag.Key);

        return Task.FromResult(Result.Success());
    }

    public Task<Result<FeatureFlag>> GetByIdAsync(
        Guid flagId,
        CancellationToken cancellationToken = default)
    {
        if (!_flags.TryGetValue(flagId, out var flag))
        {
            return Task.FromResult(Result<FeatureFlag>.Failure(
                Error.NotFound("FeatureFlag.NotFound", $"Flag {flagId} not found")));
        }

        return Task.FromResult(Result<FeatureFlag>.Success(flag));
    }

    public Task<Result<FeatureFlag>> GetByKeyAsync(
        string flagKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(flagKey);

        if (!_keyToIdMap.TryGetValue(flagKey, out var flagId) ||
            !_flags.TryGetValue(flagId, out var flag))
        {
            return Task.FromResult(Result<FeatureFlag>.Failure(
                Error.NotFound("FeatureFlag.NotFound", $"Flag with key '{flagKey}' not found")));
        }

        return Task.FromResult(Result<FeatureFlag>.Success(flag));
    }

    public Task<Result<IReadOnlyList<FeatureFlag>>> ListAsync(
        FeatureFlagFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        var query = _flags.Values.AsEnumerable();

        if (filter != null)
        {
            if (filter.Status.HasValue)
            {
                query = query.Where(f => f.Status == filter.Status.Value);
            }

            if (filter.Type.HasValue)
            {
                query = query.Where(f => f.Type == filter.Type.Value);
            }

            if (filter.IsEnabled.HasValue)
            {
                query = query.Where(f => f.IsEnabled == filter.IsEnabled.Value);
            }

            if (!string.IsNullOrEmpty(filter.TagKey))
            {
                query = query.Where(f => f.Tags.ContainsKey(filter.TagKey));

                if (!string.IsNullOrEmpty(filter.TagValue))
                {
                    query = query.Where(f =>
                        f.Tags.TryGetValue(filter.TagKey!, out var value) && value == filter.TagValue);
                }
            }

            if (!string.IsNullOrEmpty(filter.SearchTerm))
            {
                var searchLower = filter.SearchTerm.ToLowerInvariant();
                query = query.Where(f =>
                    f.Key.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                    f.Name.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                    (f.Description?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ?? false));
            }
        }

        var result = query.OrderBy(f => f.Key).ToList();
        return Task.FromResult(Result<IReadOnlyList<FeatureFlag>>.Success(result));
    }

    public Task<Result<IReadOnlyList<FeatureFlagAuditEntry>>> GetAuditHistoryAsync(
        Guid flagId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (!_flags.ContainsKey(flagId) && !_auditHistory.ContainsKey(flagId))
        {
            return Task.FromResult(Result<IReadOnlyList<FeatureFlagAuditEntry>>.Failure(
                Error.NotFound("FeatureFlag.NotFound", $"Flag {flagId} not found")));
        }

        if (_auditHistory.TryGetValue(flagId, out var history))
        {
            var result = history
                .OrderByDescending(e => e.ChangedAt)
                .Take(limit)
                .ToList();
            return Task.FromResult(Result<IReadOnlyList<FeatureFlagAuditEntry>>.Success(result));
        }

        return Task.FromResult(Result<IReadOnlyList<FeatureFlagAuditEntry>>.Success(
            Array.Empty<FeatureFlagAuditEntry>()));
    }

    private object? EvaluateFlag(FeatureFlag flag, FeatureFlagContext context)
    {
        // Evaluate rules in priority order
        var orderedRules = flag.Rules.OrderBy(r => r.Priority).ToList();

        foreach (var rule in orderedRules)
        {
            if (EvaluateRule(rule, context))
            {
                return ApplyRuleAction(rule, flag, context);
            }
        }

        // No rules matched, return default behavior
        if (flag.IsEnabled)
        {
            return flag.Type == FeatureFlagType.Boolean
                ? true
                : flag.DefaultValue;
        }

        return flag.Type == FeatureFlagType.Boolean
            ? false
            : flag.DefaultValue;
    }

    private bool EvaluateRule(FeatureFlagRule rule, FeatureFlagContext context)
    {
        if (rule.Conditions.Count == 0)
        {
            return true; // No conditions = always match
        }

        // All conditions must match (AND logic)
        return rule.Conditions.All(c => EvaluateCondition(c, context));
    }

    private bool EvaluateCondition(FeatureFlagCondition condition, FeatureFlagContext context)
    {
        var attributeValue = GetAttributeValue(condition.Attribute, context);
        if (attributeValue == null)
        {
            return false;
        }

        return condition.Operator switch
        {
            FeatureFlagOperator.Equals =>
                string.Equals(attributeValue.ToString(), condition.Value.ToString(),
                    StringComparison.OrdinalIgnoreCase),
            FeatureFlagOperator.NotEquals =>
                !string.Equals(attributeValue.ToString(), condition.Value.ToString(),
                    StringComparison.OrdinalIgnoreCase),
            FeatureFlagOperator.Contains =>
                attributeValue.ToString()!.Contains(condition.Value.ToString()!,
                    StringComparison.OrdinalIgnoreCase),
            FeatureFlagOperator.NotContains =>
                !attributeValue.ToString()!.Contains(condition.Value.ToString()!,
                    StringComparison.OrdinalIgnoreCase),
            FeatureFlagOperator.StartsWith =>
                attributeValue.ToString()!.StartsWith(condition.Value.ToString()!,
                    StringComparison.OrdinalIgnoreCase),
            FeatureFlagOperator.EndsWith =>
                attributeValue.ToString()!.EndsWith(condition.Value.ToString()!,
                    StringComparison.OrdinalIgnoreCase),
            FeatureFlagOperator.GreaterThan =>
                CompareNumeric(attributeValue, condition.Value) > 0,
            FeatureFlagOperator.LessThan =>
                CompareNumeric(attributeValue, condition.Value) < 0,
            FeatureFlagOperator.GreaterThanOrEquals =>
                CompareNumeric(attributeValue, condition.Value) >= 0,
            FeatureFlagOperator.LessThanOrEquals =>
                CompareNumeric(attributeValue, condition.Value) <= 0,
            FeatureFlagOperator.In =>
                IsInList(attributeValue, condition.Value),
            FeatureFlagOperator.NotIn =>
                !IsInList(attributeValue, condition.Value),
            FeatureFlagOperator.Matches =>
                Regex.IsMatch(attributeValue.ToString()!, condition.Value.ToString()!),
            _ => false
        };
    }

    private object? GetAttributeValue(string attribute, FeatureFlagContext context)
    {
        return attribute.ToLowerInvariant() switch
        {
            "userid" or "user_id" => context.UserId?.ToString(),
            "teamid" or "team_id" => context.TeamId?.ToString(),
            "workspaceid" or "workspace_id" => context.WorkspaceId?.ToString(),
            "environment" or "env" => context.Environment,
            _ => context.Attributes.TryGetValue(attribute, out var value) ? value : null
        };
    }

    private object? ApplyRuleAction(FeatureFlagRule rule, FeatureFlag flag, FeatureFlagContext context)
    {
        return rule.Action switch
        {
            FeatureFlagRuleAction.Enable => true,
            FeatureFlagRuleAction.Disable => false,
            FeatureFlagRuleAction.SetValue => rule.Value,
            FeatureFlagRuleAction.SetVariant => GetVariantValue(flag, rule.VariantKey),
            FeatureFlagRuleAction.Rollout => EvaluateRollout(rule, context)
                ? (rule.Value ?? true)
                : (flag.Type == FeatureFlagType.Boolean ? false : flag.DefaultValue),
            _ => flag.DefaultValue
        };
    }

    private object? GetVariantValue(FeatureFlag flag, string? variantKey)
    {
        if (string.IsNullOrEmpty(variantKey))
        {
            return flag.DefaultValue;
        }

        var variant = flag.Variants.FirstOrDefault(v => v.Key == variantKey);
        return variant?.Value ?? flag.DefaultValue;
    }

    private bool EvaluateRollout(FeatureFlagRule rule, FeatureFlagContext context)
    {
        if (!rule.RolloutPercentage.HasValue)
        {
            return true;
        }

        // Use a deterministic hash based on user/team/workspace ID
        var hashInput = $"{context.UserId}{context.TeamId}{context.WorkspaceId}{rule.Id}";
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(hashInput));
        var hashValue = Math.Abs(BitConverter.ToInt32(hash, 0)) % 100;

        return hashValue < rule.RolloutPercentage.Value;
    }

    private static int CompareNumeric(object a, object b)
    {
        if (double.TryParse(a.ToString(), out var aNum) &&
            double.TryParse(b.ToString(), out var bNum))
        {
            return aNum.CompareTo(bNum);
        }
        return string.Compare(a.ToString(), b.ToString(), StringComparison.Ordinal);
    }

    private static bool IsInList(object value, object list)
    {
        var valueStr = value.ToString();
        if (list is IEnumerable<object> enumerable)
        {
            return enumerable.Any(item =>
                string.Equals(item.ToString(), valueStr, StringComparison.OrdinalIgnoreCase));
        }

        if (list is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
        {
            return jsonElement.EnumerateArray()
                .Any(item => string.Equals(item.ToString(), valueStr, StringComparison.OrdinalIgnoreCase));
        }

        // Treat as comma-separated string
        var listStr = list.ToString();
        return listStr!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(item => string.Equals(item, valueStr, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsValidFlagKey(string key)
    {
        return Regex.IsMatch(key, @"^[a-zA-Z][a-zA-Z0-9_-]*$");
    }

    private static object GetDefaultValueForType(FeatureFlagType type)
    {
        return type switch
        {
            FeatureFlagType.Boolean => false,
            FeatureFlagType.String => string.Empty,
            FeatureFlagType.Number => 0,
            FeatureFlagType.Json => new object(),
            _ => false
        };
    }

    private FeatureFlagAuditAction DetermineAuditAction(FeatureFlag old, FeatureFlag updated)
    {
        if (old.IsEnabled != updated.IsEnabled)
        {
            return updated.IsEnabled ? FeatureFlagAuditAction.Enabled : FeatureFlagAuditAction.Disabled;
        }

        if (old.Status != updated.Status && updated.Status == FeatureFlagStatus.Archived)
        {
            return FeatureFlagAuditAction.Archived;
        }

        if (!old.Rules.SequenceEqual(updated.Rules))
        {
            return FeatureFlagAuditAction.RulesChanged;
        }

        if (!old.Variants.SequenceEqual(updated.Variants))
        {
            return FeatureFlagAuditAction.VariantsChanged;
        }

        return FeatureFlagAuditAction.Updated;
    }

    private void AddAuditEntry(Guid flagId, string flagKey, FeatureFlagAuditAction action,
        string? oldValue, string? newValue, Guid? changedBy)
    {
        var entry = new FeatureFlagAuditEntry
        {
            FlagId = flagId,
            FlagKey = flagKey,
            Action = action,
            OldValue = oldValue,
            NewValue = newValue,
            ChangedBy = changedBy
        };

        var history = _auditHistory.GetOrAdd(flagId, _ => new List<FeatureFlagAuditEntry>());
        lock (history)
        {
            history.Add(entry);
            // Keep only last 100 entries
            while (history.Count > 100)
            {
                history.RemoveAt(0);
            }
        }
    }
}
