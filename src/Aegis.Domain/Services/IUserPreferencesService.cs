using Aegis.Domain.Common;

namespace Aegis.Domain.Services;

/// <summary>
/// Service for managing user and workspace preferences
/// </summary>
public interface IUserPreferencesService
{
    /// <summary>
    /// Gets a preference value for a user
    /// </summary>
    Task<Result<T>> GetAsync<T>(
        Guid userId,
        string key,
        T defaultValue,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a preference value for a workspace
    /// </summary>
    Task<Result<T>> GetWorkspaceAsync<T>(
        Guid workspaceId,
        string key,
        T defaultValue,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a preference value for a user
    /// </summary>
    Task<Result> SetAsync<T>(
        Guid userId,
        string key,
        T value,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a preference value for a workspace
    /// </summary>
    Task<Result> SetWorkspaceAsync<T>(
        Guid workspaceId,
        string key,
        T value,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all preferences for a user
    /// </summary>
    Task<Result<UserPreferences>> GetAllAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all preferences for a workspace
    /// </summary>
    Task<Result<WorkspacePreferences>> GetAllWorkspaceAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates multiple preferences for a user at once
    /// </summary>
    Task<Result> UpdateAsync(
        Guid userId,
        Dictionary<string, object> preferences,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates multiple preferences for a workspace at once
    /// </summary>
    Task<Result> UpdateWorkspaceAsync(
        Guid workspaceId,
        Dictionary<string, object> preferences,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a preference for a user
    /// </summary>
    Task<Result> DeleteAsync(
        Guid userId,
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a preference for a workspace
    /// </summary>
    Task<Result> DeleteWorkspaceAsync(
        Guid workspaceId,
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets all preferences for a user to defaults
    /// </summary>
    Task<Result> ResetToDefaultsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the effective preference value (workspace > user > default)
    /// </summary>
    Task<Result<T>> GetEffectiveAsync<T>(
        Guid userId,
        Guid? workspaceId,
        string key,
        T defaultValue,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// User preferences container
/// </summary>
public record UserPreferences
{
    public Guid UserId { get; init; }
    public QueryPreferences Query { get; init; } = new();
    public UiPreferences Ui { get; init; } = new();
    public NotificationPreferences Notifications { get; init; } = new();
    public ExportPreferences Export { get; init; } = new();
    public LocalePreferences Locale { get; init; } = new();
    public Dictionary<string, object> Custom { get; init; } = new();
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>
/// Workspace preferences container
/// </summary>
public record WorkspacePreferences
{
    public Guid WorkspaceId { get; init; }
    public QueryPreferences Query { get; init; } = new();
    public UiPreferences Ui { get; init; } = new();
    public DataPreferences Data { get; init; } = new();
    public Dictionary<string, object> Custom { get; init; } = new();
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>
/// Query-related preferences
/// </summary>
public record QueryPreferences
{
    public string DefaultModel { get; init; } = "gpt-4";
    public double Temperature { get; init; } = 0.7;
    public double TopP { get; init; } = 0.9;
    public int MaxTokens { get; init; } = 2048;
    public int MaxResults { get; init; } = 10;
    public bool EnableReranking { get; init; } = true;
    public bool EnableCaching { get; init; } = true;
    public bool IncludeCitations { get; init; } = true;
    public string QueryMode { get; init; } = "rag"; // rag, agent, hybrid
    public double MinConfidenceScore { get; init; } = 0.5;
}

/// <summary>
/// UI-related preferences
/// </summary>
public record UiPreferences
{
    public string Theme { get; init; } = "system"; // light, dark, system
    public string DefaultView { get; init; } = "chat"; // chat, search, documents
    public bool SidebarCollapsed { get; init; } = false;
    public bool ShowTimestamps { get; init; } = true;
    public bool CompactMode { get; init; } = false;
    public int PageSize { get; init; } = 20;
    public bool EnableAnimations { get; init; } = true;
    public bool ShowSourcePreviews { get; init; } = true;
}

/// <summary>
/// Notification preferences
/// </summary>
public record NotificationPreferences
{
    public bool EmailEnabled { get; init; } = true;
    public bool SlackEnabled { get; init; } = false;
    public bool InAppEnabled { get; init; } = true;
    public bool WebhookEnabled { get; init; } = false;
    public NotificationFrequency DigestFrequency { get; init; } = NotificationFrequency.Realtime;
    public IReadOnlyList<string> MutedCategories { get; init; } = [];
    public bool QuietHoursEnabled { get; init; } = false;
    public TimeSpan? QuietHoursStart { get; init; }
    public TimeSpan? QuietHoursEnd { get; init; }
}

/// <summary>
/// Notification frequency options
/// </summary>
public enum NotificationFrequency
{
    Realtime,
    Hourly,
    Daily,
    Weekly,
    Never
}

/// <summary>
/// Export preferences
/// </summary>
public record ExportPreferences
{
    public string DefaultFormat { get; init; } = "json"; // json, csv, pdf, docx, xlsx
    public bool IncludeMetadata { get; init; } = true;
    public bool IncludeCitations { get; init; } = true;
    public bool IncludeConfidenceScores { get; init; } = true;
    public string DateFormat { get; init; } = "yyyy-MM-dd HH:mm:ss";
    public bool PrettyPrint { get; init; } = false;
}

/// <summary>
/// Locale preferences
/// </summary>
public record LocalePreferences
{
    public string Language { get; init; } = "en";
    public string Region { get; init; } = "US";
    public string Timezone { get; init; } = "UTC";
    public string DateFormat { get; init; } = "MM/dd/yyyy";
    public string TimeFormat { get; init; } = "HH:mm:ss";
    public string NumberFormat { get; init; } = "1,234.56";
}

/// <summary>
/// Data-related preferences (workspace level)
/// </summary>
public record DataPreferences
{
    public int RetentionDays { get; init; } = 365;
    public bool AutoDeleteExpired { get; init; } = false;
    public bool EnableVersioning { get; init; } = true;
    public int MaxDocumentSizeMb { get; init; } = 50;
    public IReadOnlyList<string> AllowedFileTypes { get; init; } = ["pdf", "docx", "txt", "md", "html"];
    public bool EnableOcr { get; init; } = true;
    public string DefaultChunkingStrategy { get; init; } = "semantic"; // semantic, fixed, paragraph
    public int ChunkSize { get; init; } = 512;
    public int ChunkOverlap { get; init; } = 50;
}

/// <summary>
/// Well-known preference keys
/// </summary>
public static class PreferenceKeys
{
    // Query preferences
    public const string QueryDefaultModel = "query.defaultModel";
    public const string QueryTemperature = "query.temperature";
    public const string QueryTopP = "query.topP";
    public const string QueryMaxTokens = "query.maxTokens";
    public const string QueryMaxResults = "query.maxResults";
    public const string QueryEnableReranking = "query.enableReranking";
    public const string QueryEnableCaching = "query.enableCaching";
    public const string QueryIncludeCitations = "query.includeCitations";
    public const string QueryMode = "query.mode";
    public const string QueryMinConfidence = "query.minConfidenceScore";

    // UI preferences
    public const string UiTheme = "ui.theme";
    public const string UiDefaultView = "ui.defaultView";
    public const string UiSidebarCollapsed = "ui.sidebarCollapsed";
    public const string UiShowTimestamps = "ui.showTimestamps";
    public const string UiCompactMode = "ui.compactMode";
    public const string UiPageSize = "ui.pageSize";
    public const string UiEnableAnimations = "ui.enableAnimations";
    public const string UiShowSourcePreviews = "ui.showSourcePreviews";

    // Notification preferences
    public const string NotifyEmailEnabled = "notifications.emailEnabled";
    public const string NotifySlackEnabled = "notifications.slackEnabled";
    public const string NotifyInAppEnabled = "notifications.inAppEnabled";
    public const string NotifyWebhookEnabled = "notifications.webhookEnabled";
    public const string NotifyDigestFrequency = "notifications.digestFrequency";

    // Export preferences
    public const string ExportDefaultFormat = "export.defaultFormat";
    public const string ExportIncludeMetadata = "export.includeMetadata";
    public const string ExportIncludeCitations = "export.includeCitations";

    // Locale preferences
    public const string LocaleLanguage = "locale.language";
    public const string LocaleRegion = "locale.region";
    public const string LocaleTimezone = "locale.timezone";
    public const string LocaleDateFormat = "locale.dateFormat";

    // Data preferences (workspace)
    public const string DataRetentionDays = "data.retentionDays";
    public const string DataEnableVersioning = "data.enableVersioning";
    public const string DataMaxDocumentSizeMb = "data.maxDocumentSizeMb";
    public const string DataChunkingStrategy = "data.chunkingStrategy";
    public const string DataChunkSize = "data.chunkSize";
}
