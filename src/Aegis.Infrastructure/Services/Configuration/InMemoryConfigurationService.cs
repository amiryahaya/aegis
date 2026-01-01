using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Configuration;

/// <summary>
/// In-memory implementation of configuration service
/// </summary>
public class InMemoryConfigurationService : IConfigurationService
{
    private readonly ConcurrentDictionary<string, ConfigurationEntry> _entries = new();
    private readonly ConcurrentDictionary<string, List<ConfigurationAuditEntry>> _auditHistory = new();
    private readonly ILogger<InMemoryConfigurationService> _logger;

    public InMemoryConfigurationService(ILogger<InMemoryConfigurationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        InitializeDefaults();
    }

    public Task<Result<T>> GetAsync<T>(
        string key,
        T defaultValue,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_entries.TryGetValue(key, out var entry))
        {
            return Task.FromResult(Result<T>.Success(ConvertValue<T>(entry.Value, defaultValue)));
        }

        return Task.FromResult(Result<T>.Success(defaultValue));
    }

    public Task<Result<T>> GetAsync<T>(
        ConfigurationCategory category,
        string key,
        T defaultValue,
        CancellationToken cancellationToken = default)
    {
        var fullKey = $"{category.ToString().ToLowerInvariant()}.{key}";
        return GetAsync(fullKey, defaultValue, cancellationToken);
    }

    public async Task<Result> SetAsync<T>(
        string key,
        T value,
        Guid? changedBy = null,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var validationResult = await ValidateAsync(key, value, cancellationToken);
        if (validationResult.IsFailure)
        {
            return validationResult;
        }

        var category = ConfigurationDefaults.GetCategory(key);
        var oldEntry = _entries.TryGetValue(key, out var existing) ? existing : null;
        var oldValue = oldEntry?.Value != null ? JsonSerializer.Serialize(oldEntry.Value) : null;

        var newEntry = new ConfigurationEntry
        {
            Key = key,
            Category = category,
            Value = value!,
            ValueType = DetermineValueType<T>(),
            DefaultValue = ConfigurationDefaults.Values.TryGetValue(key, out var def) ? def : null,
            CreatedAt = oldEntry?.CreatedAt ?? DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = changedBy
        };

        _entries[key] = newEntry;

        var action = oldEntry == null ? ConfigurationAuditAction.Created : ConfigurationAuditAction.Updated;
        AddAuditEntry(key, category, action, oldValue, JsonSerializer.Serialize(value), changedBy, reason);

        _logger.LogInformation("Configuration {Key} set to {Value}", key, value);

        return Result.Success();
    }

    public Task<Result> SetAsync<T>(
        ConfigurationCategory category,
        string key,
        T value,
        Guid? changedBy = null,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var fullKey = $"{category.ToString().ToLowerInvariant()}.{key}";
        return SetAsync(fullKey, value, changedBy, reason, cancellationToken);
    }

    public Task<Result<IReadOnlyList<ConfigurationEntry>>> GetByCategoryAsync(
        ConfigurationCategory category,
        CancellationToken cancellationToken = default)
    {
        var entries = _entries.Values
            .Where(e => e.Category == category)
            .OrderBy(e => e.Key)
            .ToList();

        return Task.FromResult(Result<IReadOnlyList<ConfigurationEntry>>.Success(entries));
    }

    public Task<Result<IReadOnlyList<ConfigurationEntry>>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var entries = _entries.Values
            .OrderBy(e => e.Category)
            .ThenBy(e => e.Key)
            .ToList();

        return Task.FromResult(Result<IReadOnlyList<ConfigurationEntry>>.Success(entries));
    }

    public Task<Result> DeleteAsync(
        string key,
        Guid? changedBy = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (!_entries.TryRemove(key, out var entry))
        {
            return Task.FromResult(Result.Failure(
                Error.NotFound("Configuration.NotFound", $"Configuration key '{key}' not found")));
        }

        AddAuditEntry(key, entry.Category, ConfigurationAuditAction.Deleted,
            JsonSerializer.Serialize(entry.Value), null, changedBy, null);

        _logger.LogInformation("Configuration {Key} deleted", key);

        return Task.FromResult(Result.Success());
    }

    public Task<Result> ResetCategoryAsync(
        ConfigurationCategory category,
        Guid? changedBy = null,
        CancellationToken cancellationToken = default)
    {
        var categoryPrefix = category.ToString().ToLowerInvariant() + ".";
        var keysToReset = _entries.Keys
            .Where(k => k.StartsWith(categoryPrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var key in keysToReset)
        {
            if (ConfigurationDefaults.Values.TryGetValue(key, out var defaultValue))
            {
                var oldValue = _entries.TryGetValue(key, out var entry)
                    ? JsonSerializer.Serialize(entry.Value)
                    : null;

                _entries[key] = new ConfigurationEntry
                {
                    Key = key,
                    Category = category,
                    Value = defaultValue,
                    DefaultValue = defaultValue,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = changedBy
                };

                AddAuditEntry(key, category, ConfigurationAuditAction.Reset,
                    oldValue, JsonSerializer.Serialize(defaultValue), changedBy, "Reset to defaults");
            }
            else
            {
                _entries.TryRemove(key, out _);
            }
        }

        _logger.LogInformation("Configuration category {Category} reset to defaults", category);

        return Task.FromResult(Result.Success());
    }

    public Task<Result<IReadOnlyList<ConfigurationAuditEntry>>> GetAuditHistoryAsync(
        string? key = null,
        ConfigurationCategory? category = null,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var allEntries = _auditHistory.Values
            .SelectMany(list => list)
            .AsEnumerable();

        if (!string.IsNullOrEmpty(key))
        {
            allEntries = allEntries.Where(e => e.Key == key);
        }

        if (category.HasValue)
        {
            allEntries = allEntries.Where(e => e.Category == category.Value);
        }

        var result = allEntries
            .OrderByDescending(e => e.ChangedAt)
            .Take(limit)
            .ToList();

        return Task.FromResult(Result<IReadOnlyList<ConfigurationAuditEntry>>.Success(result));
    }

    public Task<Result> ValidateAsync<T>(
        string key,
        T value,
        CancellationToken cancellationToken = default)
    {
        if (_entries.TryGetValue(key, out var entry))
        {
            if (entry.IsReadOnly)
            {
                return Task.FromResult(Result.Failure(
                    Error.Validation("Configuration.ReadOnly", $"Configuration key '{key}' is read-only")));
            }

            if (entry.AllowedValues != null && entry.AllowedValues.Count > 0)
            {
                var valueStr = value?.ToString();
                if (!entry.AllowedValues.Any(v => v.ToString() == valueStr))
                {
                    return Task.FromResult(Result.Failure(
                        Error.Validation("Configuration.InvalidValue",
                            $"Value must be one of: {string.Join(", ", entry.AllowedValues)}")));
                }
            }

            if (entry.MinValue != null || entry.MaxValue != null)
            {
                if (double.TryParse(value?.ToString(), out var numValue))
                {
                    if (entry.MinValue != null && double.TryParse(entry.MinValue.ToString(), out var min) &&
                        numValue < min)
                    {
                        return Task.FromResult(Result.Failure(
                            Error.Validation("Configuration.BelowMinimum",
                                $"Value must be at least {min}")));
                    }

                    if (entry.MaxValue != null && double.TryParse(entry.MaxValue.ToString(), out var max) &&
                        numValue > max)
                    {
                        return Task.FromResult(Result.Failure(
                            Error.Validation("Configuration.AboveMaximum",
                                $"Value must be at most {max}")));
                    }
                }
            }

            if (!string.IsNullOrEmpty(entry.ValidationPattern))
            {
                var valueStr = value?.ToString() ?? string.Empty;
                if (!Regex.IsMatch(valueStr, entry.ValidationPattern))
                {
                    return Task.FromResult(Result.Failure(
                        Error.Validation("Configuration.PatternMismatch",
                            $"Value does not match required pattern")));
                }
            }
        }

        return Task.FromResult(Result.Success());
    }

    public async Task<Result<int>> ImportAsync(
        Dictionary<string, object> configuration,
        bool overwrite = false,
        Guid? changedBy = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var imported = 0;

        foreach (var (key, value) in configuration)
        {
            if (!overwrite && _entries.ContainsKey(key))
            {
                continue;
            }

            var result = await SetAsync(key, value, changedBy, "Imported", cancellationToken);
            if (result.IsSuccess)
            {
                imported++;
            }
        }

        _logger.LogInformation("Imported {Count} configuration entries", imported);

        return Result<int>.Success(imported);
    }

    public Task<Result<Dictionary<string, object>>> ExportAsync(
        ConfigurationCategory? category = null,
        CancellationToken cancellationToken = default)
    {
        var query = _entries.Values.AsEnumerable();

        if (category.HasValue)
        {
            query = query.Where(e => e.Category == category.Value);
        }

        // Exclude secrets from export
        query = query.Where(e => !e.IsSecret);

        var result = query.ToDictionary(e => e.Key, e => e.Value);

        return Task.FromResult(Result<Dictionary<string, object>>.Success(result));
    }

    private void InitializeDefaults()
    {
        foreach (var (key, value) in ConfigurationDefaults.Values)
        {
            var category = ConfigurationDefaults.GetCategory(key);
            _entries[key] = new ConfigurationEntry
            {
                Key = key,
                Category = category,
                Value = value,
                ValueType = DetermineValueType(value),
                DefaultValue = value
            };
        }

        _logger.LogDebug("Initialized {Count} default configuration entries", _entries.Count);
    }

    private static T ConvertValue<T>(object value, T defaultValue)
    {
        try
        {
            if (value is T typedValue)
            {
                return typedValue;
            }

            if (value is JsonElement jsonElement)
            {
                var converted = JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
                return converted ?? defaultValue;
            }

            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }

    private static ConfigurationValueType DetermineValueType<T>()
    {
        return DetermineValueType(typeof(T));
    }

    private static ConfigurationValueType DetermineValueType(object value)
    {
        return DetermineValueType(value.GetType());
    }

    private static ConfigurationValueType DetermineValueType(Type type)
    {
        if (type == typeof(bool))
            return ConfigurationValueType.Boolean;
        if (type == typeof(int) || type == typeof(long))
            return ConfigurationValueType.Integer;
        if (type == typeof(double) || type == typeof(float) || type == typeof(decimal))
            return ConfigurationValueType.Double;
        if (type == typeof(TimeSpan))
            return ConfigurationValueType.Duration;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            return ConfigurationValueType.StringList;
        if (type == typeof(JsonElement) || type.IsClass && type != typeof(string))
            return ConfigurationValueType.Json;

        return ConfigurationValueType.String;
    }

    private void AddAuditEntry(
        string key,
        ConfigurationCategory category,
        ConfigurationAuditAction action,
        string? oldValue,
        string? newValue,
        Guid? changedBy,
        string? reason)
    {
        var entry = new ConfigurationAuditEntry
        {
            Key = key,
            Category = category,
            Action = action,
            OldValue = oldValue,
            NewValue = newValue,
            ChangedBy = changedBy,
            Reason = reason
        };

        var history = _auditHistory.GetOrAdd(key, _ => new List<ConfigurationAuditEntry>());
        lock (history)
        {
            history.Add(entry);
            // Keep only last 100 entries per key
            while (history.Count > 100)
            {
                history.RemoveAt(0);
            }
        }
    }
}
