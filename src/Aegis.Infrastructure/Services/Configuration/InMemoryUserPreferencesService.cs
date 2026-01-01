using System.Collections.Concurrent;
using System.Text.Json;
using Aegis.Domain.Common;
using Aegis.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Aegis.Infrastructure.Services.Configuration;

/// <summary>
/// In-memory implementation of user preferences service
/// </summary>
public class InMemoryUserPreferencesService : IUserPreferencesService
{
    private readonly ConcurrentDictionary<Guid, Dictionary<string, object>> _userPreferences = new();
    private readonly ConcurrentDictionary<Guid, Dictionary<string, object>> _workspacePreferences = new();
    private readonly ConcurrentDictionary<Guid, DateTime> _userUpdatedAt = new();
    private readonly ConcurrentDictionary<Guid, DateTime> _workspaceUpdatedAt = new();
    private readonly ILogger<InMemoryUserPreferencesService> _logger;

    public InMemoryUserPreferencesService(ILogger<InMemoryUserPreferencesService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<Result<T>> GetAsync<T>(
        Guid userId,
        string key,
        T defaultValue,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_userPreferences.TryGetValue(userId, out var prefs) &&
            prefs.TryGetValue(key, out var value))
        {
            return Task.FromResult(Result<T>.Success(ConvertValue<T>(value, defaultValue)));
        }

        return Task.FromResult(Result<T>.Success(defaultValue));
    }

    public Task<Result<T>> GetWorkspaceAsync<T>(
        Guid workspaceId,
        string key,
        T defaultValue,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_workspacePreferences.TryGetValue(workspaceId, out var prefs) &&
            prefs.TryGetValue(key, out var value))
        {
            return Task.FromResult(Result<T>.Success(ConvertValue<T>(value, defaultValue)));
        }

        return Task.FromResult(Result<T>.Success(defaultValue));
    }

    public Task<Result> SetAsync<T>(
        Guid userId,
        string key,
        T value,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var prefs = _userPreferences.GetOrAdd(userId, _ => new Dictionary<string, object>());
        lock (prefs)
        {
            prefs[key] = value!;
        }

        _userUpdatedAt[userId] = DateTime.UtcNow;
        _logger.LogDebug("Set user preference {Key} for user {UserId}", key, userId);

        return Task.FromResult(Result.Success());
    }

    public Task<Result> SetWorkspaceAsync<T>(
        Guid workspaceId,
        string key,
        T value,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var prefs = _workspacePreferences.GetOrAdd(workspaceId, _ => new Dictionary<string, object>());
        lock (prefs)
        {
            prefs[key] = value!;
        }

        _workspaceUpdatedAt[workspaceId] = DateTime.UtcNow;
        _logger.LogDebug("Set workspace preference {Key} for workspace {WorkspaceId}", key, workspaceId);

        return Task.FromResult(Result.Success());
    }

    public Task<Result<UserPreferences>> GetAllAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var prefs = _userPreferences.GetOrAdd(userId, _ => new Dictionary<string, object>());
        var updatedAt = _userUpdatedAt.TryGetValue(userId, out var date) ? date : (DateTime?)null;

        var userPrefs = new UserPreferences
        {
            UserId = userId,
            Query = BuildQueryPreferences(prefs),
            Ui = BuildUiPreferences(prefs),
            Notifications = BuildNotificationPreferences(prefs),
            Export = BuildExportPreferences(prefs),
            Locale = BuildLocalePreferences(prefs),
            Custom = GetCustomPreferences(prefs),
            UpdatedAt = updatedAt
        };

        return Task.FromResult(Result<UserPreferences>.Success(userPrefs));
    }

    public Task<Result<WorkspacePreferences>> GetAllWorkspaceAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        var prefs = _workspacePreferences.GetOrAdd(workspaceId, _ => new Dictionary<string, object>());
        var updatedAt = _workspaceUpdatedAt.TryGetValue(workspaceId, out var date) ? date : (DateTime?)null;

        var workspacePrefs = new WorkspacePreferences
        {
            WorkspaceId = workspaceId,
            Query = BuildQueryPreferences(prefs),
            Ui = BuildUiPreferences(prefs),
            Data = BuildDataPreferences(prefs),
            Custom = GetCustomPreferences(prefs),
            UpdatedAt = updatedAt
        };

        return Task.FromResult(Result<WorkspacePreferences>.Success(workspacePrefs));
    }

    public Task<Result> UpdateAsync(
        Guid userId,
        Dictionary<string, object> preferences,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        var prefs = _userPreferences.GetOrAdd(userId, _ => new Dictionary<string, object>());
        lock (prefs)
        {
            foreach (var (key, value) in preferences)
            {
                prefs[key] = value;
            }
        }

        _userUpdatedAt[userId] = DateTime.UtcNow;
        _logger.LogInformation("Updated {Count} preferences for user {UserId}", preferences.Count, userId);

        return Task.FromResult(Result.Success());
    }

    public Task<Result> UpdateWorkspaceAsync(
        Guid workspaceId,
        Dictionary<string, object> preferences,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        var prefs = _workspacePreferences.GetOrAdd(workspaceId, _ => new Dictionary<string, object>());
        lock (prefs)
        {
            foreach (var (key, value) in preferences)
            {
                prefs[key] = value;
            }
        }

        _workspaceUpdatedAt[workspaceId] = DateTime.UtcNow;
        _logger.LogInformation("Updated {Count} preferences for workspace {WorkspaceId}",
            preferences.Count, workspaceId);

        return Task.FromResult(Result.Success());
    }

    public Task<Result> DeleteAsync(
        Guid userId,
        string key,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_userPreferences.TryGetValue(userId, out var prefs))
        {
            lock (prefs)
            {
                prefs.Remove(key);
            }
            _userUpdatedAt[userId] = DateTime.UtcNow;
        }

        return Task.FromResult(Result.Success());
    }

    public Task<Result> DeleteWorkspaceAsync(
        Guid workspaceId,
        string key,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_workspacePreferences.TryGetValue(workspaceId, out var prefs))
        {
            lock (prefs)
            {
                prefs.Remove(key);
            }
            _workspaceUpdatedAt[workspaceId] = DateTime.UtcNow;
        }

        return Task.FromResult(Result.Success());
    }

    public Task<Result> ResetToDefaultsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        _userPreferences.TryRemove(userId, out _);
        _userUpdatedAt.TryRemove(userId, out _);

        _logger.LogInformation("Reset preferences to defaults for user {UserId}", userId);

        return Task.FromResult(Result.Success());
    }

    public async Task<Result<T>> GetEffectiveAsync<T>(
        Guid userId,
        Guid? workspaceId,
        string key,
        T defaultValue,
        CancellationToken cancellationToken = default)
    {
        // Priority: workspace > user > default

        if (workspaceId.HasValue)
        {
            var workspaceResult = await GetWorkspaceAsync<T>(workspaceId.Value, key, default!, cancellationToken);
            if (workspaceResult.IsSuccess && workspaceResult.Value != null &&
                !EqualityComparer<T>.Default.Equals(workspaceResult.Value, default!))
            {
                return workspaceResult;
            }
        }

        var userResult = await GetAsync<T>(userId, key, default!, cancellationToken);
        if (userResult.IsSuccess && userResult.Value != null &&
            !EqualityComparer<T>.Default.Equals(userResult.Value, default!))
        {
            return userResult;
        }

        return Result<T>.Success(defaultValue);
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

    private static QueryPreferences BuildQueryPreferences(Dictionary<string, object> prefs)
    {
        var defaults = new QueryPreferences();
        return new QueryPreferences
        {
            DefaultModel = GetValue(prefs, PreferenceKeys.QueryDefaultModel, defaults.DefaultModel),
            Temperature = GetValue(prefs, PreferenceKeys.QueryTemperature, defaults.Temperature),
            TopP = GetValue(prefs, PreferenceKeys.QueryTopP, defaults.TopP),
            MaxTokens = GetValue(prefs, PreferenceKeys.QueryMaxTokens, defaults.MaxTokens),
            MaxResults = GetValue(prefs, PreferenceKeys.QueryMaxResults, defaults.MaxResults),
            EnableReranking = GetValue(prefs, PreferenceKeys.QueryEnableReranking, defaults.EnableReranking),
            EnableCaching = GetValue(prefs, PreferenceKeys.QueryEnableCaching, defaults.EnableCaching),
            IncludeCitations = GetValue(prefs, PreferenceKeys.QueryIncludeCitations, defaults.IncludeCitations),
            QueryMode = GetValue(prefs, PreferenceKeys.QueryMode, defaults.QueryMode),
            MinConfidenceScore = GetValue(prefs, PreferenceKeys.QueryMinConfidence, defaults.MinConfidenceScore)
        };
    }

    private static UiPreferences BuildUiPreferences(Dictionary<string, object> prefs)
    {
        var defaults = new UiPreferences();
        return new UiPreferences
        {
            Theme = GetValue(prefs, PreferenceKeys.UiTheme, defaults.Theme),
            DefaultView = GetValue(prefs, PreferenceKeys.UiDefaultView, defaults.DefaultView),
            SidebarCollapsed = GetValue(prefs, PreferenceKeys.UiSidebarCollapsed, defaults.SidebarCollapsed),
            ShowTimestamps = GetValue(prefs, PreferenceKeys.UiShowTimestamps, defaults.ShowTimestamps),
            CompactMode = GetValue(prefs, PreferenceKeys.UiCompactMode, defaults.CompactMode),
            PageSize = GetValue(prefs, PreferenceKeys.UiPageSize, defaults.PageSize),
            EnableAnimations = GetValue(prefs, PreferenceKeys.UiEnableAnimations, defaults.EnableAnimations),
            ShowSourcePreviews = GetValue(prefs, PreferenceKeys.UiShowSourcePreviews, defaults.ShowSourcePreviews)
        };
    }

    private static NotificationPreferences BuildNotificationPreferences(Dictionary<string, object> prefs)
    {
        var defaults = new NotificationPreferences();
        return new NotificationPreferences
        {
            EmailEnabled = GetValue(prefs, PreferenceKeys.NotifyEmailEnabled, defaults.EmailEnabled),
            SlackEnabled = GetValue(prefs, PreferenceKeys.NotifySlackEnabled, defaults.SlackEnabled),
            InAppEnabled = GetValue(prefs, PreferenceKeys.NotifyInAppEnabled, defaults.InAppEnabled),
            WebhookEnabled = GetValue(prefs, PreferenceKeys.NotifyWebhookEnabled, defaults.WebhookEnabled),
            DigestFrequency = GetValue(prefs, PreferenceKeys.NotifyDigestFrequency, defaults.DigestFrequency)
        };
    }

    private static ExportPreferences BuildExportPreferences(Dictionary<string, object> prefs)
    {
        var defaults = new ExportPreferences();
        return new ExportPreferences
        {
            DefaultFormat = GetValue(prefs, PreferenceKeys.ExportDefaultFormat, defaults.DefaultFormat),
            IncludeMetadata = GetValue(prefs, PreferenceKeys.ExportIncludeMetadata, defaults.IncludeMetadata),
            IncludeCitations = GetValue(prefs, PreferenceKeys.ExportIncludeCitations, defaults.IncludeCitations)
        };
    }

    private static LocalePreferences BuildLocalePreferences(Dictionary<string, object> prefs)
    {
        var defaults = new LocalePreferences();
        return new LocalePreferences
        {
            Language = GetValue(prefs, PreferenceKeys.LocaleLanguage, defaults.Language),
            Region = GetValue(prefs, PreferenceKeys.LocaleRegion, defaults.Region),
            Timezone = GetValue(prefs, PreferenceKeys.LocaleTimezone, defaults.Timezone),
            DateFormat = GetValue(prefs, PreferenceKeys.LocaleDateFormat, defaults.DateFormat)
        };
    }

    private static DataPreferences BuildDataPreferences(Dictionary<string, object> prefs)
    {
        var defaults = new DataPreferences();
        return new DataPreferences
        {
            RetentionDays = GetValue(prefs, PreferenceKeys.DataRetentionDays, defaults.RetentionDays),
            EnableVersioning = GetValue(prefs, PreferenceKeys.DataEnableVersioning, defaults.EnableVersioning),
            MaxDocumentSizeMb = GetValue(prefs, PreferenceKeys.DataMaxDocumentSizeMb, defaults.MaxDocumentSizeMb),
            DefaultChunkingStrategy = GetValue(prefs, PreferenceKeys.DataChunkingStrategy, defaults.DefaultChunkingStrategy),
            ChunkSize = GetValue(prefs, PreferenceKeys.DataChunkSize, defaults.ChunkSize)
        };
    }

    private static Dictionary<string, object> GetCustomPreferences(Dictionary<string, object> prefs)
    {
        var wellKnownKeys = typeof(PreferenceKeys)
            .GetFields()
            .Select(f => f.GetValue(null)?.ToString())
            .Where(k => k != null)
            .ToHashSet();

        return prefs
            .Where(kvp => !wellKnownKeys.Contains(kvp.Key))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    private static T GetValue<T>(Dictionary<string, object> prefs, string key, T defaultValue)
    {
        if (prefs.TryGetValue(key, out var value))
        {
            return ConvertValue(value, defaultValue);
        }
        return defaultValue;
    }
}
